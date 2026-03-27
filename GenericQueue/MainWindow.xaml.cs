using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;
using System.Collections.Specialized;
using System.Collections;
//using Excel = Microsoft.Office.Interop.Excel;
//using Excel;//DataReader;
using CsvHelper;

//known issues:
//reset scroll bars to top//possibly fixed?

namespace GenericQueue
{
    public partial class MainWindow : Window
    {

        ObservableCollection<string> TypeList = new ObservableCollection<string>();
        DataTable backupOne = new DataTable();
        DataTable backupTwo = new DataTable();
        DataTable FirstDT = new DataTable();
        DataTable SecondDT;
        DataTable ResponseDT = new DataTable();
        List<Field> OrderedFields = new List<Field>();
        string User = string.Empty;
        public int ClickedRowID { get; set; }
        private string ClickedContents;
        SqlConnection Connection;
        DataSet TypeTable = new DataSet();
        public int ActiveTypeIndex;


        public int SelectedIndex;
        public int DeselectedIndex;
        public string UploadsFolder = string.Empty;
        public int ButtonID;
        public bool AllowProcessAll = true;
        public bool AllowImport = true;
        private bool IsDemo = false;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception e)
            {
                LogError(e);
            }
        }

        private static string AcquireConnectString()
        {

            XmlDocument doc = new XmlDocument();
            doc.Load(System.IO.Path.Combine(Environment.CurrentDirectory, "db_config.xml"));
            XmlNode cNode = doc.DocumentElement.SelectSingleNode("/connect");
            string server = cNode.SelectSingleNode("server").InnerText;
            string database = cNode.SelectSingleNode("database").InnerText;
            string username = cNode.SelectSingleNode("username").InnerText;
            string password = cNode.SelectSingleNode("password").InnerText;
            return "Data Source=" + server +
                                ";Initial Catalog=" + database +
                                ";User Id=" + username +
                                ";Password=" + password +
                                ";Trusted_Connection=False" +
                                ";Connection Timeout=15";
        }

        public static SqlConnection ConnectToDB()
        {
            var ConnectionString = AcquireConnectString();
            SqlConnection Connection = new SqlConnection(ConnectionString);
            Connection.Open();

            return Connection;
        }

        private void SetBusy(bool busy, string message = "Loading...")
        {
            BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            BusyText.Text = message;
            FillButton.IsEnabled = !busy && TypeDropdown.SelectedIndex >= 0;
            TypeDropdown.IsEnabled = !busy;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                DeselectedIndex = -1;
                SelectedIndex = -1;
                FromTB.Visibility = Visibility.Collapsed;
                ToTB.Visibility = Visibility.Collapsed;
                FromDatePicker.Visibility = Visibility.Collapsed;
                ToDatePicker.Visibility = Visibility.Collapsed;

                SetBusy(true, "Connecting...");
                try
                {
                    var loaded = await Task.Run(() =>
                    {
                        var cs = AcquireConnectString();
                        using (var c = new SqlConnection(cs))
                        {
                            c.Open();
                            var ds = new DataSet();
                            var da = new SqlDataAdapter("Select * from dbo.q_Type", c);
                            da.SelectCommand.CommandTimeout = 30;
                            da.Fill(ds);
                            return Tuple.Create(ds, Environment.UserName, cs);
                        }
                    });
                    TypeTable = loaded.Item1;
                    User = loaded.Item2;
                    Connection = new SqlConnection(loaded.Item3);
                    foreach (DataRow r in TypeTable.Tables[0].Rows)
                        TypeList.Add(r.ItemArray[1].ToString());
                    TypeDropdown.ItemsSource = TypeList;
                }
                catch (Exception)
                {
                    var result = MessageBox.Show(
                        "Could not connect to the database. Load demo data instead?",
                        "Database Unavailable",
                        MessageBoxButton.YesNo);
                    if (result == MessageBoxResult.Yes)
                    {
                        IsDemo = true;
                        LoadDemoTypes();
                    }
                }
                finally
                {
                    SetBusy(false);
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private void TypeDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FillButton.IsEnabled = true;
        }

        private async Task GetFirstGridDataAsync()
        {
            if (IsDemo)
            {
                LoadDemoFirstGrid();
                GenerateFirstGrid();
                return;
            }

            var type = TypeDropdown.Items[ActiveTypeIndex].ToString();
            var user = User;
            var date1 = FromDatePicker.SelectedDate;
            var date2 = ToDatePicker.SelectedDate;
            var connStr = Connection.ConnectionString;

            SetBusy(true, "Loading...");
            try
            {
                var dt = await Task.Run(() =>
                {
                    using (var c = new SqlConnection(connStr))
                    {
                        c.Open();
                        var result = new DataTable();
                        var cmd = new SqlCommand("dbo.q_Load_List", c);
                        cmd.CommandTimeout = 30;
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@user", user));
                        cmd.Parameters.Add(new SqlParameter("@type", type));
                        if (date1 != null)
                            cmd.Parameters.Add(new SqlParameter("@date1", date1.ToString()));
                        if (date2 != null)
                            cmd.Parameters.Add(new SqlParameter("@date2", date2.ToString()));
                        result.Load(cmd.ExecuteReader());
                        return result;
                    }
                });
                FirstDT = dt;
                backupOne = FirstDT.Copy();
                GenerateFirstGrid();
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void GenerateFirstGrid(bool regenerating = false)
        {
            try
            {
                if (regenerating)
                    FirstDT = backupOne.Copy();
                DeselectedIndex = -1;
                SelectedIndex = -1;
                ButtonID = -1;
                FirstGrid.ItemsSource = null;
                FirstGrid.Columns.Clear();
                FirstGrid.Items.Clear();
                FirstGrid.Items.Refresh();
                FieldsPanel.Children.Clear();
                List<string> colsToKeep = new List<string>();
                for (int i = 0; i < FirstDT.Columns.Count; i++)
                {
                    if (FirstDT.Columns[i].ColumnName.StartsWith("button_"))
                    {
                        ButtonID = i;
                        var label = FirstDT.Columns[i].ColumnName.Remove(0, 7);
                        DataGridTemplateColumn col1 = new DataGridTemplateColumn();
                        col1.Header = label;
                        FrameworkElementFactory factory1 = new FrameworkElementFactory(typeof(ExButton));
                        Binding b = new Binding(FirstDT.Columns[i].ColumnName);
                        Binding b1 = new Binding("id");

                        factory1.SetValue(ExButton.ContentProperty, label);
                        factory1.SetValue(ExButton.TextProperty, b);
                        factory1.SetValue(ExButton.IDProperty, b1);

                        factory1.AddHandler(Button.ClickEvent, (RoutedEventHandler)DG_Button_Click);
                        DataTemplate cellTemplate1 = new DataTemplate();

                        DataTrigger trig = new DataTrigger
                        {
                            Binding = new Binding() { Path = new PropertyPath("Text"), RelativeSource = RelativeSource.Self },
                            Value = string.Empty
                        };
                        Style style = new Style
                        {
                            TargetType = typeof(ExButton)
                        };
                        Setter setter = new Setter
                        {
                            Property = ExButton.VisibilityProperty,
                            Value = Visibility.Hidden
                        };
                        trig.Setters.Add(setter);
                        style.Triggers.Clear();
                        style.Triggers.Add(trig);
                        DataTrigger activeTrig = new DataTrigger
                        {
                            Binding = new Binding() { Path = new PropertyPath("IsActive"), RelativeSource = RelativeSource.Self },
                            Value = true
                        };
                        Style activeStyle = new Style
                        {
                            TargetType = typeof(ExButton)
                        };
                        Setter activeSetter = new Setter
                        {
                            Property = TextBlock.TextDecorationsProperty,
                            Value = TextDecorations.Underline
                        };
                        style.Triggers.Add(activeTrig);
                        factory1.SetValue(ExButton.StyleProperty, style);
                        cellTemplate1.Triggers.Add(trig);
                        col1.CellTemplate = cellTemplate1;
                        cellTemplate1.VisualTree = factory1;
                        col1.CanUserSort = false;
                        FirstGrid.Columns.Add(col1);
                        continue;
                    }
                    if (FirstDT.Columns[i].ColumnName.StartsWith("document_"))
                    {
                        var label = FirstDT.Columns[i].ColumnName.Remove(0, 9);
                        FrameworkElementFactory tbFactory = new FrameworkElementFactory(typeof(TextBlock));
                        Binding b = new Binding(FirstDT.Columns[i].ColumnName);
                        tbFactory.SetBinding(TextBlock.TextProperty, b);

                        FrameworkElementFactory hyperlinkFactory = new FrameworkElementFactory(typeof(Hyperlink));
                        hyperlinkFactory.AppendChild(tbFactory);
                        hyperlinkFactory.SetBinding(Hyperlink.NavigateUriProperty, b);
                        hyperlinkFactory.AddHandler(Hyperlink.ClickEvent, (RoutedEventHandler)DG_Hyperlink_Click);
                        FrameworkElementFactory tb2Factory = new FrameworkElementFactory(typeof(TextBlock));
                        tb2Factory.AppendChild(hyperlinkFactory);

                        //FrameworkElementFactory browseButtonFactory = new FrameworkElementFactory(typeof(ExButton));
                        //browseButtonFactory.SetValue(ExButton.ContentProperty, "Browse...");
                        //browseButtonFactory.AddHandler(Button.ClickEvent, (RoutedEventHandler)DG_Browse_Click);
                        //browseButtonFactory.SetValue(ExButton.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                        //browseButtonFactory.SetBinding(ExButton.TextProperty, b);
                        //DataTrigger trig = new DataTrigger
                        //{
                        //    Binding = new Binding() { Path = new PropertyPath("Text"), RelativeSource = RelativeSource.Self },
                        //    Value = string.Empty
                        //};
                        //Style style = new Style
                        //{
                        //    TargetType = typeof(ExButton)
                        //};
                        //Setter setter = new Setter
                        //{
                        //    Property = ExButton.VisibilityProperty,
                        //    Value = Visibility.Hidden
                        //};
                        //trig.Setters.Add(setter);
                        //style.Triggers.Clear();
                        //style.Triggers.Add(trig);
                        //browseButtonFactory.SetValue(ExButton.StyleProperty, style);

                        DataGridTemplateColumn dgc = new DataGridTemplateColumn();
                        dgc.Header = label;
                        dgc.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);

                        FrameworkElementFactory sb = new FrameworkElementFactory(typeof(StackPanel));
                        sb.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                        sb.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
                        sb.AppendChild(tb2Factory);
                        Binding colorBinding = new Binding("color");
                        colorBinding.Converter = new ColorConverter();
                        sb.SetValue(StackPanel.BackgroundProperty, colorBinding);
                        //sb.AppendChild(browseButtonFactory);
                        DataTemplate dataTemplate = new DataTemplate { VisualTree = sb };
                        //dataTemplate.Triggers.Add(trig);
                        dgc.CellTemplate = dataTemplate;
                        dgc.CanUserSort = false;
                        FirstGrid.Columns.Add(dgc);
                        continue;
                    }
                    if (FirstDT.Columns[i].ColumnName == "id" || FirstDT.Columns[i].ColumnName.ToLower().Equals("color"))
                    {
                        DataGridTextColumn col1 = new DataGridTextColumn();
                        col1.Visibility = Visibility.Hidden;
                        col1.CanUserSort = false;
                        FirstGrid.Columns.Add(col1);
                        continue;
                    }
                    if (FirstDT.Columns[i].DataType.Equals(typeof(Boolean)))
                    {
                        DataGridCheckBoxColumn col1 = new DataGridCheckBoxColumn();
                        col1.Header = FirstDT.Columns[i].ColumnName;
                        col1.SortMemberPath = FirstDT.Columns[i].ColumnName;
                        col1.Binding = new Binding(FirstDT.Columns[i].ColumnName);
                        col1.IsReadOnly = true;
                        col1.CanUserSort = false;
                        FirstGrid.Columns.Add(col1);
                        continue;
                    }
                    else
                    {
                        DataGridTextColumn col1 = new DataGridTextColumn();
                        col1.Header = FirstDT.Columns[i].ColumnName;
                        col1.SortMemberPath = FirstDT.Columns[i].ColumnName;

                        Binding colorBinding = new Binding("color");
                        colorBinding.Converter = new ColorConverter();

                        Style columnStyle = new Style(typeof(TextBlock));
                        columnStyle.Triggers.Clear();
                        Setter s = new Setter(
                                TextBlock.BackgroundProperty,
                                colorBinding);
                        columnStyle.Setters.Add(s);


                        //DataTrigger activeTrig = new DataTrigger();
                        //activeTrig.Binding = new Binding() { 
                        //    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataRowView), 1),
                        //    Converter = new EqualityConverter(this) };
                        //activeTrig.Value = true;
                        ////{
                        ////    Binding = new Binding() { Path = new PropertyPath("SelectedIndex"), ElementName = "QWindow" },//RelativeSource = RelativeSource.TemplatedParent },
                        ////    Value = 1
                        ////};
                        //Style activeStyle = new Style
                        //{
                        //    TargetType = typeof(TextBlock)
                        //};
                        //Setter activeSetter = new Setter
                        //{
                        //    Property = TextBlock.TextDecorationsProperty,
                        //    Value = TextDecorations.Underline
                        //};
                        //activeStyle.Setters.Add(activeSetter);
                        //activeStyle.Triggers.Add(activeTrig);
                        //activeStyle.Setters.Add(s);
                        //col1.ElementStyle = columnStyle;
                        col1.ElementStyle = columnStyle;// CellStyle.Triggers.Add(ac);
                        ////col1.SetValue(DataGridTextColumn.ForegroundProperty, colorBinding);
                        col1.Binding = new Binding(FirstDT.Columns[i].ColumnName);
                        col1.IsReadOnly = true;
                        col1.CanUserSort = true;
                        col1.SortMemberPath = FirstDT.Columns[i].ColumnName;
                        FirstGrid.Columns.Add(col1);
                        continue;
                    }

                }
                FirstGrid.ItemsSource = FirstDT.DefaultView;
                RowsCountTB.Text = FirstGrid.Items.Count.ToString();
                BuildFilterPanel();
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private void DGButton(DataRow row)
        {
            try
            {
                ClickedRowID = (int)row.ItemArray[FirstDT.Columns["id"].Ordinal];
                ClickedContents = row.ItemArray[FirstDT.Columns[ButtonID].Ordinal].ToString();
                if (IsDemo)
                {
                    LoadDemoSecondGrid();
                    return;
                }
                SecondDT = new DataTable();
                Connection.Open();
                SqlCommand cmd = new SqlCommand("dbo.q_Load_Details", Connection);
                cmd.CommandTimeout = 30;
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@user", User));
                cmd.Parameters.Add(new SqlParameter("@type", TypeDropdown.Items[ActiveTypeIndex].ToString()));
                cmd.Parameters.Add(new SqlParameter("@id", ClickedRowID));
                cmd.Parameters.Add(new SqlParameter("@contents", ClickedContents));
                SqlDataReader rdr = cmd.ExecuteReader();
                SecondDT.Load(rdr);
                backupTwo = SecondDT.Copy();
                Connection.Close();
            }
            catch (Exception ex)
            {
                LogError(ex);
            }

        }

        private async void DG_Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SelectedIndex = FirstGrid.SelectedIndex;
                if (DeselectedIndex >= 0 && SelectedIndex != DeselectedIndex)
                {

                    var deselRow = (DataGridRow)FirstGrid.ItemContainerGenerator
                                                     .ContainerFromIndex(DeselectedIndex);

                    //DataGridCell cell = 
                    for (int i = 0; i < FirstDT.Columns.Count; i++)
                    {
                        var item = FirstGrid.Columns[i].GetCellContent(deselRow);//.Parent;// as DataGridCell;
                        if (item != null && item.GetType().Name.Equals(typeof(TextBlock).Name))
                        {
                            Binding colorBinding = new Binding("color");
                            colorBinding.Converter = new ColorConverter();

                            Style columnStyle = new Style(typeof(TextBlock));
                            columnStyle.Triggers.Clear();
                            Setter s = new Setter(
                                    TextBlock.BackgroundProperty,
                                    colorBinding);
                            columnStyle.Setters.Add(s);


                            //DataTrigger activeTrig = new DataTrigger();
                            //activeTrig.Binding = new Binding() { 
                            //    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataRowView), 1),
                            //    Converter = new EqualityConverter(this) };
                            //activeTrig.Value = true;
                            ////{
                            ////    Binding = new Binding() { Path = new PropertyPath("SelectedIndex"), ElementName = "QWindow" },//RelativeSource = RelativeSource.TemplatedParent },
                            ////    Value = 1
                            ////};
                            //Style activeStyle = new Style
                            //{
                            //    TargetType = typeof(TextBlock)
                            //};
                            //Setter activeSetter = new Setter
                            //{
                            //    Property = TextBlock.TextDecorationsProperty,
                            //    Value = TextDecorations.Underline
                            //};
                            //activeStyle.Setters.Add(activeSetter);
                            //activeStyle.Triggers.Add(activeTrig);
                            //activeStyle.Setters.Add(s);
                            //col1.ElementStyle = columnStyle;
                            item.Style = columnStyle;
                        }
                    }
                    //FirstGrid.UpdateLayout();
                    //cell.Style = 
                    // Applied logic
                    //row.FontFamily = new FontFamily()
                    deselRow.FontWeight = FontWeights.Normal;

                    UpdateLayout();
                }
                DeselectedIndex = SelectedIndex;
                ClickedRowID = (sender as ExButton).ID;


                //FirstGrid.AutoGeneratedColumns += (s, ex) =>
                //{
                //    FirstGrid.RowStyle..Columns[FirstGrid.Columns.Count - 1].CellStyle = this.Resources["CellStyle"] as Style;
                //    FirstGrid.Columns[0].CellStyle = this.Resources["CellStyle"] as Style;
                //};
                //DataTrigger activeTrig = new DataTrigger();
                //activeTrig.Binding = new Binding()
                //{
                //    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataRowView), 1),
                //    Converter = new EqualityConverter(this)
                //};
                //activeTrig.Value = true;
                //{
                //    Binding = new Binding() { Path = new PropertyPath("SelectedIndex"), ElementName = "QWindow" },//RelativeSource = RelativeSource.TemplatedParent },
                //    Value = 1
                //};
                Style activeStyle = new Style
                {
                    TargetType = typeof(TextBlock)
                };
                Setter activeSetter = new Setter
                {
                    Property = TextBlock.TextDecorationsProperty,
                    Value = TextDecorations.Underline
                };
                //Setter activeSetter2 = new Setter
                //{
                //    Property = TextBlock.TextDecorationsProperty,
                //    //Value = TextDecorations.
                //};
                Binding cb = new Binding("color");
                cb.Converter = new ColorConverter();

                Setter s1 = new Setter(
                                    TextBlock.BackgroundProperty,
                                    cb);
                activeStyle.Setters.Add(activeSetter);
                activeStyle.Setters.Add(s1);

                var row = (DataGridRow)FirstGrid.ItemContainerGenerator
                                                 .ContainerFromIndex(SelectedIndex);

                //DataGridCell cell = 
                for (int i = 0; i < FirstDT.Columns.Count; i++)
                {
                    var item = FirstGrid.Columns[i].GetCellContent(row);//.Parent;// as DataGridCell;
                    if (item != null && item.GetType().Name.Equals(typeof(TextBlock).Name))
                    {
                        item.Style = activeStyle;
                    }
                }
                //FirstGrid.UpdateLayout();
                //cell.Style = 
                // Applied logic
                //row.FontFamily = new FontFamily()
                row.FontWeight = FontWeights.Bold;
                //row.FontStyle = FontStyles.Oblique;
                //col1.ElementStyle = columnStyle;
                //.ElementStyle = activeStyle;
                ClickedContents = (sender as ExButton).Text;
                if (IsDemo)
                {
                    LoadDemoSecondGrid();
                    GenerateSecondGrid();
                }
                else
                {
                    var user = User;
                    var type = TypeDropdown.Items[ActiveTypeIndex].ToString();
                    var id = ClickedRowID;
                    var contents = ClickedContents;
                    var connStr = Connection.ConnectionString;
                    SetBusy(true, "Loading details...");
                    try
                    {
                        var dt = await Task.Run(() =>
                        {
                            using (var c = new SqlConnection(connStr))
                            {
                                c.Open();
                                var result = new DataTable();
                                var cmd = new SqlCommand("dbo.q_Load_Details", c);
                                cmd.CommandTimeout = 30;
                                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                                cmd.Parameters.Add(new SqlParameter("@user", user));
                                cmd.Parameters.Add(new SqlParameter("@type", type));
                                cmd.Parameters.Add(new SqlParameter("@id", id));
                                cmd.Parameters.Add(new SqlParameter("@contents", contents));
                                result.Load(cmd.ExecuteReader());
                                return result;
                            }
                        });
                        SecondDT = dt;
                        backupTwo = SecondDT.Copy();
                        GenerateSecondGrid();
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                    }
                    finally
                    {
                        SetBusy(false);
                    }
                }
                
            }
            catch (Exception ex)
            {
                LogError(ex);
            }

        }

        private void GenerateSecondGrid(bool regenerating = false)
        {
            OrderedFields = new List<Field>();
            try
            {
                if (SecondDT == null || SecondDT.Rows.Count < 1)
                    return;
                if (regenerating)
                    SecondDT = backupTwo.Copy();
                FieldsPanel.Children.Clear();
                FieldsPanelScroller.ScrollToHome();
                SaveButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
                Border nb = new Border()
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = Brushes.LightGray
                };
                nb.Child = new TextBlock { Text = "Name", Margin = new Thickness(5, 3, 5, 3) };

                Border vb = new Border()
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = Brushes.LightGray
                };
                vb.Child = new TextBlock { Text = "Value", Margin = new Thickness(5, 3, 5, 3) };
                FieldsPanel.Children.Add(nb);
                FieldsPanel.Children.Add(vb);
                Grid.SetRow(nb, 0);
                Grid.SetRow(vb, 0);
                Grid.SetColumn(nb, 0);
                Grid.SetColumn(vb, 1);
                EnumCollection enums = new EnumCollection();
                var xml = SecondDT.Rows[0]?.Field<string>("details")?.ToString()?.ToByteArray();
                var enumsXml = SecondDT.Rows[0]?.Field<string>("enums")?.ToString()?.ToByteArray();
                MemoryStream stream = xml == null ? new MemoryStream() : new MemoryStream(xml);
                MemoryStream enumStream = enumsXml == null ? new MemoryStream() : new MemoryStream(enumsXml);
                using (TextReader reader = new StreamReader(stream), enumReader = new StreamReader(enumStream))
                {
                    if (reader.Peek() == -1)
                        return;
                    XmlSerializer serializer = new XmlSerializer(typeof(FieldCollection));
                    var stuff = (FieldCollection)serializer.Deserialize(reader);

                    if (stuff.Fields.Count() > 0)
                        FieldsPanel.Visibility = Visibility.Visible;
                    if (enumReader.Peek() != -1)
                    {
                        XmlSerializer enumSerializer = new XmlSerializer(typeof(EnumCollection));
                        enums = (EnumCollection)enumSerializer.Deserialize(enumReader);

                    }

                    int i = 0;
                    var newList = stuff.Fields.ToList().OrderBy(f => f.Order).ToList();
                    newList.ForEach(s => OrderedFields.Add(s));
                    foreach (Field f in newList)
                    {
                        i++;
                        if (string.IsNullOrEmpty(f.Label))
                            continue;
                        FieldsPanel.RowDefinitions.Add(new RowDefinition());
                        SolidColorBrush solidColorBrush = null;
                        try
                        {
                            solidColorBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#" + f.Color.ToLower()));
                        }
                        catch (Exception ex) { }
                        Border b = new Border()
                        {
                            BorderBrush = Brushes.Black,
                            BorderThickness = new Thickness(1, 1, 1, 1),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            Background = solidColorBrush
                        };
                        b.Child = new TextBlock { Text = f.Label, Margin = new Thickness(5, 3, 5, 3) };
                        FieldsPanel.Children.Add(b);

                        Border b1 = new Border()
                        {
                            BorderBrush = Brushes.Black,
                            BorderThickness = new Thickness(1, 1, 1, 1),
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };

                        object child = null;

                        if (f.DataType.ToLower().Equals("string"))
                            if (f.ReadOnly.Equals(1) || f.ReadOnly.ToString().ToLower().Equals("true"))
                                child = new TextBlock { Text = f.Value, Margin = new Thickness(3, 0, 5, 0), Padding = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
                            else
                                child = new TextBox { Text = f.Value, Padding = new Thickness(5, 1, 5, 1), VerticalAlignment = VerticalAlignment.Center };

                        else if (f.DataType.ToLower().Equals("enum"))
                        {
                            var source = enums.Enums?.ToList().Where(e => e.Name.Equals(f.Name))?.ToList();

                            var selected = source == null ? null : source.Where(e => e.Value.Equals(f.Value))?.FirstOrDefault();

                            if (!f.ReadOnly)
                            {

                                child = new ComboBox
                                {
                                    ItemsSource = source,
                                    DisplayMemberPath = "Label",
                                    SelectedItem = selected,
                                    IsHitTestVisible = !f.ReadOnly,
                                };
                            }
                            else
                                child = new TextBlock { Text = source == null ? "" : source.Where(e => e.Value.Equals(f.Value))?.Select(p => p.Label).FirstOrDefault(), Margin = new Thickness(3, 0, 5, 0), Padding = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
                        }


                        else if (f.DataType.ToLower().Equals("path"))
                            child = GetLinkCell(f);

                        else if (f.DataType.ToLower().Equals("date"))
                            child = GetDateCell(f);

                        else if (f.DataType.ToLower().Equals("bool") || f.DataType.ToLower().Equals("boolean"))
                        {
                            child = new CheckBox { IsChecked = f.Value.Equals("true") || f.Value.Equals("1") ? true : false, IsHitTestVisible = !f.ReadOnly, VerticalAlignment = VerticalAlignment.Center };
                            if (f.ReadOnly)
                                b1.SetValue(BackgroundProperty, Brushes.LightGray);
                        }


                        if (child != null)
                            b1.Child = (UIElement)child;
                        FieldsPanel.Children.Add(b1);
                        Grid.SetRow(b, i);
                        Grid.SetRow(b1, i);
                        Grid.SetColumn(b, 0);
                        Grid.SetColumn(b1, 1);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private DatePicker GetDateCell(Field f)
        {
            try
            {
                if (string.IsNullOrEmpty(f.Value))
                    return new DatePicker { };
                StackPanel sp = new StackPanel();
                sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                DatePicker calendar = new DatePicker();
                calendar.SelectedDate = DateTime.Parse(f.Value);
                calendar.IsEnabled = !f.ReadOnly;
                //Calendar calendar = new Calendar();
                //calendar.SetValue(DatePicker.SelectedDateProperty, f.Value);
                //if (f.ReadOnly)
                //{
                //    calendar.SetValue(DatePicker.IsHitTestVisibleProperty, f.ReadOnly);
                //}

                //Hyperlink hl = new Hyperlink();
                //Uri uri = new Uri(f.Value);
                //hl.SetValue(Hyperlink.NavigateUriProperty, uri);
                //hl.Inlines.Add(textBlock);
                //hl.AddHandler(Hyperlink.ClickEvent, (RoutedEventHandler)DG_Hyperlink_Click);
                //TextBlock tb = new TextBlock();
                //tb.Inlines.Add(hl);
                //tb.Margin = new Thickness(5, 0, 10, 0);
                //tb.VerticalAlignment = VerticalAlignment.Center;
                //Button b = new Button();
                //b.HorizontalAlignment = HorizontalAlignment.Right;
                //b.Content = "Browse...";
                //b.AddHandler(Button.ClickEvent, (RoutedEventHandler)DG_Browse_Click);
                //if (!f.ReadOnly)
                //    sp.Children.Add(b);
                //sp.Children.Add(calendar);

                return calendar;
            }
            catch (Exception ex)
            {
                LogError(ex);
                return null;
            }
        }

        private StackPanel GetLinkCell(Field f)
        {
            try
            {
                StackPanel sp = new StackPanel();
                Button b = new Button();

                b.Content = "Browse...";
                b.AddHandler(Button.ClickEvent, (RoutedEventHandler)DG_Browse_Click);
                if (!f.ReadOnly)
                    sp.Children.Add(b);
                //if (!string.IsNullOrEmpty(f.Value))
                //{
                TextBlock textBlock = new TextBlock();
                textBlock.SetValue(TextBlock.TextProperty, f?.Value);

                sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                Hyperlink hl = new Hyperlink();
                if (!string.IsNullOrEmpty(f.Value))
                {
                    Uri uri = new Uri(f?.Value);
                    hl.SetValue(Hyperlink.NavigateUriProperty, uri);
                }

                hl.Inlines.Add(textBlock);
                hl.AddHandler(Hyperlink.ClickEvent, (RoutedEventHandler)DG_Hyperlink_Click);
                TextBlock tb = new TextBlock();
                tb.Inlines.Add(hl);
                tb.Margin = new Thickness(5, 0, 10, 0);
                tb.VerticalAlignment = VerticalAlignment.Center;
                sp.Children.Add(tb);
                //}
                //else
                //b.HorizontalAlignment = HorizontalAlignment.Left;



                return sp;
            }
            catch (Exception ex)
            {
                LogError(ex);
                return null;
            }
        }

        private void DG_Browse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var s = ((sender as Button).Parent as StackPanel).Children.OfType<TextBlock>().FirstOrDefault();
                OpenFileDialog dialog = new OpenFileDialog();
                var result = dialog.ShowDialog();
                if ((bool)result)
                {

                    Hyperlink hl = new Hyperlink();
                    var newPath = dialog.FileName;
                    if (!string.IsNullOrEmpty(UploadsFolder))
                    {
                        newPath = System.IO.Path.Combine(UploadsFolder, System.IO.Path.GetFileName(dialog.FileName));
                        if (File.Exists(newPath))
                        {
                            newPath = System.IO.Path.Combine(UploadsFolder, System.IO.Path.GetFileNameWithoutExtension(dialog.FileName) + DateTime.Now.ToString("-MMddyyHHmmss") + System.IO.Path.GetExtension(dialog.FileName));
                        }
                        s?.Inlines?.Clear();
                        File.Copy(dialog.FileName, newPath);
                    }

                    Uri uri = new Uri(newPath);
                    hl.SetValue(Hyperlink.NavigateUriProperty, uri);
                    hl.Inlines.Add(newPath);
                    hl.AddHandler(Hyperlink.ClickEvent, (RoutedEventHandler)DG_Hyperlink_Click);
                    s.Inlines.Add(hl);
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private void DG_Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Hyperlink link = (Hyperlink)e.OriginalSource;
                Process.Start(link.NavigateUri.LocalPath);
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private bool Save()
        {
            try
            {
                List<Field> f = new List<Field>();
                string v = string.Empty;
                var i = SecondDT.Columns.Count;
                for (int j = 1; j < i - 1; j++)
                {
                    foreach (DataRow row in SecondDT.Rows)
                    {

                        v = row.ItemArray[j].ToString();
                        var field = OrderedFields.ElementAt<Field>(j - 1);
                        field.Value = v ?? "";
                        f.Add(field);
                    }
                }

                using (var sww = new StringWriter())
                {
                    using (XmlWriter writer = XmlWriter.Create(sww))
                    {
                        ResponseDT = new DataTable();
                        FieldCollection c = new FieldCollection();
                        c.Fields = f.ToArray();
                        XmlSerializer serializer = new XmlSerializer(typeof(FieldCollection));
                        serializer.Serialize(writer, c);
                        var x = sww.ToString();

                        ResponseDT = new DataTable();
                        Connection.Open();
                        SqlCommand cmd = new SqlCommand("dbo.q_Save_Details", Connection);
                        cmd.CommandTimeout = 30;
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.Add(new SqlParameter("@user", User));
                        cmd.Parameters.Add(new SqlParameter("@type", TypeDropdown.Items[ActiveTypeIndex].ToString()));
                        cmd.Parameters.Add(new SqlParameter("@id", ClickedRowID));
                        cmd.Parameters.Add(new SqlParameter("@contents", ClickedContents));
                        cmd.Parameters.Add(new SqlParameter("@xml", x));
                        SqlDataReader rdr = cmd.ExecuteReader();
                        ResponseDT.Load(rdr);
                        Connection.Close();
                    }

                }
                var message = ResponseDT.Rows[0][1].ToString();
                if (!string.IsNullOrEmpty(message))
                {
                    MessageBox.Show(message);
                    MessageBoxResult dialogResult = MessageBox.Show("Press 'Yes' to modify and resubmit this item, 'No' to skip it.", "Choose your own adventure.", MessageBoxButton.YesNo);
                    if (dialogResult == MessageBoxResult.Yes)
                    {
                        return true;
                    }
                    else if (dialogResult == MessageBoxResult.No)
                    {
                        return false;
                    }
                }
                return false;
                //var refresh = ResponseDT.Rows[0][0].ToString();
                //if (refresh.Equals("True"))
                //    GetFirstGridData();
            }
            catch (Exception ex)
            {
                LogError(ex);
                MessageBox.Show("Something went wrong. Check error log for more details.");
                return true;
            }
        }

        private List<Field> CollectFieldValues()
        {
            var f = new List<Field>();
            var childrenEnumerator = FieldsPanel.Children.GetEnumerator();
            while (childrenEnumerator.MoveNext())
            {
                var current = childrenEnumerator.Current;
                var c = Grid.GetColumn(current as UIElement);
                var r = Grid.GetRow(current as UIElement);
                if (r != 0)
                {
                    if (r > OrderedFields.Count)
                        continue;
                    if (c == 1)
                    {
                        DependencyProperty dp = TextBlock.TextProperty;
                        var chi = (current as Border).Child;
                        string v = string.Empty;
                        if (chi.GetType().Name.Equals(typeof(ComboBox).Name))
                        {
                            dp = ComboBox.SelectedValueProperty;
                            v = (chi.GetValue(dp) as Enum)?.Value?.ToString();
                        }
                        else if (chi.GetType().Name.Equals(typeof(CheckBox).Name))
                        {
                            dp = CheckBox.IsCheckedProperty;
                            v = chi.GetValue(dp)?.ToString();
                        }
                        else if (chi.GetType().Name.Equals(typeof(DatePicker).Name))
                        {
                            dp = DatePicker.SelectedDateProperty;
                            var i = ((DateTime)chi.GetValue(dp));
                            v = i != null ? i.Date != null ? i.Date.ToString("MM/dd/yyyy") : "" : "";
                        }
                        else if (chi.GetType().Name.Equals(typeof(StackPanel).Name))
                        {
                            var hl = (chi as StackPanel).Children.OfType<TextBlock>().FirstOrDefault();
                            if (hl == null)
                                continue;
                            v = (hl.Inlines.FirstInline as Hyperlink)?.NavigateUri?.ToString().Replace("/", "\\").Remove(0, 5);
                            Regex rgx = new Regex(@"[A-Z]:\\");
                            if (!string.IsNullOrEmpty(v))
                            {
                                var result = rgx.Match(v, 0);
                                if (result != null && result.Success)
                                    v = v.Remove(0, result.Index);
                            }
                        }
                        else
                        {
                            var fieldtest = OrderedFields.ElementAt<Field>(r - 1);
                            if (!fieldtest.ReadOnly)
                                dp = TextBox.TextProperty;
                            v = chi.GetValue(dp)?.ToString();
                        }

                        var field = OrderedFields.ElementAt<Field>(r - 1);
                        field.Value = v ?? "";
                        f.Add(field);
                    }
                }
            }
            return f;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SecondDT == null)
                    return;
                if (IsDemo)
                {
                    MessageBox.Show("Demo mode: changes are not saved to a database.", "Demo Mode");
                    return;
                }

                var fields = CollectFieldValues();
                var type = TypeDropdown.Items[ActiveTypeIndex].ToString();
                var clickedRowId = ClickedRowID;
                var clickedContents = ClickedContents;
                var user = User;
                var connStr = Connection.ConnectionString;

                SetBusy(true, "Saving...");
                Tuple<string, bool> saveResult = null;
                try
                {
                    saveResult = await Task.Run(() =>
                    {
                        string xml;
                        using (var sw = new StringWriter())
                        using (var xw = XmlWriter.Create(sw))
                        {
                            new XmlSerializer(typeof(FieldCollection)).Serialize(xw, new FieldCollection { Fields = fields.ToArray() });
                            xml = sw.ToString();
                        }
                        using (var c = new SqlConnection(connStr))
                        {
                            c.Open();
                            var cmd = new SqlCommand("dbo.q_Save_Details", c);
                            cmd.CommandTimeout = 30;
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd.Parameters.Add(new SqlParameter("@user", user));
                            cmd.Parameters.Add(new SqlParameter("@type", type));
                            cmd.Parameters.Add(new SqlParameter("@id", clickedRowId));
                            cmd.Parameters.Add(new SqlParameter("@contents", clickedContents));
                            cmd.Parameters.Add(new SqlParameter("@xml", xml));
                            var dt = new DataTable();
                            dt.Load(cmd.ExecuteReader());
                            return Tuple.Create(dt.Rows[0][1]?.ToString(), "True".Equals(dt.Rows[0][0]?.ToString()));
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogError(ex);
                }
                finally
                {
                    SetBusy(false);
                }

                if (saveResult == null) return;
                if (!string.IsNullOrEmpty(saveResult.Item1))
                    MessageBox.Show(saveResult.Item1);
                if (saveResult.Item2)
                    await GetFirstGridDataAsync();
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SecondDT != null)
                {
                    GenerateSecondGrid(regenerating: true);
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        public static void LogError(Exception error)
        {
            try
            {
                string lP = System.IO.Path.Combine(Environment.CurrentDirectory, "Errors.log");
                if (!File.Exists(lP))
                    File.Create(lP);

                StreamWriter writer = new StreamWriter(lP, append: true);
                writer.WriteLine(System.DateTime.Now.ToString());
                writer.WriteLine("----- " + error.Message);
                writer.WriteLine("----- " + error.InnerException);
                writer.WriteLine("------------------------------------");
                writer.Close();
                MessageBox.Show(error.Message + "\n" + error.InnerException);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception while writing to error log" + e);
            }
        }

        private async void FillButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ActiveTypeIndex = TypeDropdown.SelectedIndex;
                ClickedRowID = -1;
                SelectedIndex = -1;
                DeselectedIndex = -1;
                FillButton.IsEnabled = true;
                FromDatePicker.SelectedDate = null;
                ToDatePicker.SelectedDate = null;
                FromTB.Visibility = Visibility.Collapsed;
                ToTB.Visibility = Visibility.Collapsed;
                FromDatePicker.Visibility = Visibility.Collapsed;
                ToDatePicker.Visibility = Visibility.Collapsed;
                ProcessAllButton.Visibility = Visibility.Collapsed;
                AllowImportButton.Visibility = Visibility.Collapsed;

                UploadsFolder = string.Empty;
                foreach (DataRow r in TypeTable.Tables[0].Rows)
                {
                    if (r.ItemArray[1].ToString().Equals(TypeDropdown.Items[ActiveTypeIndex].ToString()))
                    {
                        if (!string.IsNullOrEmpty(r.ItemArray[4]?.ToString()))
                            if ((bool)r.ItemArray[4])
                                ProcessAllButton.Visibility = Visibility.Visible;

                        if (!string.IsNullOrEmpty(r.ItemArray[5]?.ToString()))
                            if ((bool)r.ItemArray[5])
                                AllowImportButton.Visibility = Visibility.Visible;

                        if (r.ItemArray[2] != null && !string.IsNullOrEmpty(r.ItemArray[2].ToString()) && Boolean.Parse(r.ItemArray[2].ToString()))
                        {
                            FromTB.Visibility = Visibility.Visible;
                            ToTB.Visibility = Visibility.Visible;
                            FromDatePicker.Visibility = Visibility.Visible;
                            ToDatePicker.Visibility = Visibility.Visible;
                        }
                        UploadsFolder = r.ItemArray[3] != null && !string.IsNullOrEmpty(r.ItemArray[3].ToString()) ? r.ItemArray[3].ToString() : string.Empty;
                    }

                }

                FieldsPanel.Children.Clear();
                SaveButton.IsEnabled = false;
                CancelButton.IsEnabled = false;

#if DEBUG
                if (TypeDropdown.SelectedItem != null && TypeDropdown.SelectedItem.ToString() == "--- SIMULATED ---")
                {
                    LoadSimulatedData();
                    return;
                }
#endif
                //GetFirstGridData();
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
            await GetFirstGridDataAsync();
        }

#if DEBUG
        private void LoadSimulatedData()
        {
            try
            {
                FirstDT = new DataTable();
                FirstDT.Columns.Add("id", typeof(int));
                FirstDT.Columns.Add("color", typeof(string));
                FirstDT.Columns.Add("Name", typeof(string));
                FirstDT.Columns.Add("Status", typeof(string));
                FirstDT.Columns.Add("button_Process", typeof(string));
                FirstDT.Columns.Add("document_View", typeof(string));
                FirstDT.Columns.Add("Active", typeof(bool));

                Random rand = new Random();
                string[] colors = { "White", "LightBlue", "LightGreen", "LightYellow", "Pink" };
                string[] names = { "John Doe", "Jane Smith", "Bob Jones", "Alice Brown", "Charlie Davis" };
                string[] statuses = { "Pending", "Active", "Completed", "Error", "On Hold" };

                for (int i = 1; i <= 20; i++)
                {
                    DataRow row = FirstDT.NewRow();
                    row["id"] = i;
                    row["color"] = colors[rand.Next(colors.Length)];
                    row["Name"] = names[rand.Next(names.Length)] + " " + i;
                    row["Status"] = statuses[rand.Next(statuses.Length)];
                    row["button_Process"] = "Process";
                    row["document_View"] = "http://example.com/doc/" + i;
                    row["Active"] = rand.Next(2) == 0;
                    FirstDT.Rows.Add(row);
                }

                backupOne = FirstDT.Copy();
                GenerateFirstGrid();
                RowsCountTB.Text = FirstGrid.Items.Count.ToString();
                MessageBox.Show("Loaded 20 simulated rows.");
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }
#endif

        private void ProcessAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (DataRow item in FirstDT.Rows)
            {
                DGButton(item);
                if (Save())
                {
                    GenerateFirstGrid();
                    break;
                }

            }
        }

        private void AllowImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = "csv files(*.csv)|*.csv";
                dialog.Multiselect = false;
                var result = dialog.ShowDialog();
                if ((bool)result)
                {
                    ProcessImport(dialog.FileName);
                }
            }
            catch (Exception ex) { LogError(ex); }
        }

        private void ProcessImport(string fileName)
        {
            try
            {
                string q = string.Empty;
                string temp;

                using (var reader = new StreamReader(fileName))
                using (var csv = new CsvReader(reader))
                {

                    using (var dr = new CsvDataReader(csv))
                    {
                        DataRow cols;

                        int currentRow = 0;
                        int maxcols = 0;

                        var inputTable = new DataTable();
                        inputTable.Load(dr);

                        try
                        {
                            cols = inputTable.Rows[currentRow];
                            temp = string.Empty;// "[csv_path] VARCHAR(MAX)";

                            maxcols = cols.ItemArray.Length;

                            for (int i = 0; i < cols.ItemArray.Length; i++)
                            {
                                temp += "[" + inputTable.Columns[i] + "] VARCHAR(MAX),";
                            }
                            temp = temp.TrimEnd(',');
                            q = "CREATE TABLE #t (" + temp + ")";
                            Connection.Open();
                            SqlCommand myCommand = new SqlCommand(q, Connection);
                            myCommand.ExecuteNonQuery();
                            StringBuilder query = new StringBuilder(8192);
                            for (; currentRow < inputTable.Rows.Count; currentRow++)
                            {
                                cols = inputTable.Rows[currentRow];

                                query.Append("INSERT INTO #t VALUES");
                                query.Append("('");

                                for (int j = 0; j < maxcols; j++)
                                {

                                    if (j < cols.ItemArray.Length)
                                        query.Append(cols.ItemArray[j].ToString());

                                    query.Append('\'');
                                    query.Append(",'");
                                }
                                query.Remove(query.Length - 2, 2);
                                query.Append(')');
                                query.Append(';');

                                SqlCommand command = new SqlCommand(query.ToString(), Connection);
                                command.ExecuteNonQuery();
                                
                            }
                            inputTable.Dispose();

                            //-------------------------------
                            FirstDT = new DataTable();
                            SqlCommand cmd = new SqlCommand("dbo.q_Load_Import", Connection);
                            cmd.CommandTimeout = 30;
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd.Parameters.Add(new SqlParameter("@user", User));
                            cmd.Parameters.Add(new SqlParameter("@type", TypeDropdown.Items[ActiveTypeIndex].ToString()));
                            if (FromDatePicker.SelectedDate != null)
                                cmd.Parameters.Add(new SqlParameter("@date1", FromDatePicker.SelectedDate.ToString()));
                            if (ToDatePicker.SelectedDate != null)
                                cmd.Parameters.Add(new SqlParameter("@date2", ToDatePicker.SelectedDate.ToString()));
                            cmd.ExecuteNonQuery();

                            Connection.Close();

                            GetFirstGridData();
                        }
                        catch (Exception e)
                        {
                            LogError(e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
        }
        private void BuildFilterPanel()
        {
            FilterPanel.Children.Clear();
            if (FirstDT == null || FirstDT.Columns.Count == 0)
            {
                FilterPanel.Visibility = Visibility.Collapsed;
                return;
            }

            bool hasFilters = false;
            for (int i = 0; i < FirstDT.Columns.Count; i++)
            {
                var col = FirstDT.Columns[i];
                string name = col.ColumnName;
                if (name == "id" || name.ToLower() == "color" ||
                    name.StartsWith("button_") || name.StartsWith("document_"))
                    continue;

                hasFilters = true;

                bool isDateCol = col.DataType == typeof(DateTime) ||
                    (col.DataType == typeof(string) &&
                     name.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0 &&
                     FirstDT.AsEnumerable().Select(r => r[name]?.ToString()).Where(v => !string.IsNullOrEmpty(v)).Any(v => DateTime.TryParse(v, out _)));

                if (isDateCol)
                {
                    FilterPanel.Children.Add(new TextBlock { Text = name + " From:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 2, 2, 2) });
                    string dateKind = col.DataType == typeof(DateTime) ? "fromdt" : "fromstr";
                    var fromPicker = new DatePicker { Height = 25, Width = 120, Margin = new Thickness(0, 2, 5, 2), Tag = dateKind + "|" + name };
                    fromPicker.SelectedDateChanged += FilterControl_Changed;
                    FilterPanel.Children.Add(fromPicker);

                    FilterPanel.Children.Add(new TextBlock { Text = "To:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 2, 2, 2) });
                    string toKind = col.DataType == typeof(DateTime) ? "todt" : "tostr";
                    var toPicker = new DatePicker { Height = 25, Width = 120, Margin = new Thickness(0, 2, 10, 2), Tag = toKind + "|" + name };
                    toPicker.SelectedDateChanged += FilterControl_Changed;
                    FilterPanel.Children.Add(toPicker);
                }
                else if (col.DataType == typeof(bool))
                {
                    FilterPanel.Children.Add(new TextBlock { Text = name + ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 2, 2, 2) });
                    var cb = new ComboBox { Height = 25, Width = 80, Margin = new Thickness(0, 2, 10, 2), Tag = "bool|" + name };
                    cb.Items.Add("All");
                    cb.Items.Add("True");
                    cb.Items.Add("False");
                    cb.SelectedIndex = 0;
                    cb.SelectionChanged += FilterControl_Changed;
                    FilterPanel.Children.Add(cb);
                }
                else
                {
                    FilterPanel.Children.Add(new TextBlock { Text = name + ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 2, 2, 2) });
                    var cb = new ComboBox { Height = 25, MinWidth = 80, Margin = new Thickness(0, 2, 10, 2), Tag = "text|" + name };
                    cb.Items.Add("All");
                    foreach (var v in FirstDT.AsEnumerable().Select(r => r[name]?.ToString() ?? "").Distinct().OrderBy(v => v))
                        cb.Items.Add(v);
                    cb.SelectedIndex = 0;
                    cb.SelectionChanged += FilterControl_Changed;
                    FilterPanel.Children.Add(cb);
                }
            }

            if (hasFilters)
            {
                var clearBtn = new Button { Content = "Clear Filters", Height = 25, Margin = new Thickness(10, 2, 5, 2) };
                clearBtn.Click += ClearFilters_Click;
                FilterPanel.Children.Add(clearBtn);
                FilterPanel.Visibility = Visibility.Visible;
            }
            else
                FilterPanel.Visibility = Visibility.Collapsed;
        }

        private void FilterControl_Changed(object sender, EventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            try
            {
                var filters = new List<string>();
                foreach (UIElement el in FilterPanel.Children)
                {
                    string tag = null;
                    if (el is ComboBox cb && cb.Tag is string t1) tag = t1;
                    else if (el is DatePicker dp && dp.Tag is string t2) tag = t2;
                    else continue;

                    var parts = tag.Split('|');
                    if (parts.Length != 2) continue;
                    string kind = parts[0];
                    string colName = parts[1];

                    if (kind == "text")
                    {
                        var selected = ((ComboBox)el).SelectedItem?.ToString();
                        if (string.IsNullOrEmpty(selected) || selected == "All") continue;
                        filters.Add($"[{colName}] = '{selected.Replace("'", "''")}'");
                    }
                    else if (kind == "bool")
                    {
                        var selected = ((ComboBox)el).SelectedItem?.ToString();
                        if (string.IsNullOrEmpty(selected) || selected == "All") continue;
                        filters.Add($"[{colName}] = {selected.ToLower()}");
                    }
                    else if (kind == "fromdt")
                    {
                        var picker = (DatePicker)el;
                        if (picker.SelectedDate == null) continue;
                        filters.Add($"[{colName}] >= #{picker.SelectedDate.Value:MM/dd/yyyy}#");
                    }
                    else if (kind == "todt")
                    {
                        var picker = (DatePicker)el;
                        if (picker.SelectedDate == null) continue;
                        filters.Add($"[{colName}] <= #{picker.SelectedDate.Value:MM/dd/yyyy}#");
                    }
                    else if (kind == "fromstr")
                    {
                        var picker = (DatePicker)el;
                        if (picker.SelectedDate == null) continue;
                        filters.Add($"Convert([{colName}], System.DateTime) >= #{picker.SelectedDate.Value:MM/dd/yyyy}#");
                    }
                    else if (kind == "tostr")
                    {
                        var picker = (DatePicker)el;
                        if (picker.SelectedDate == null) continue;
                        filters.Add($"Convert([{colName}], System.DateTime) <= #{picker.SelectedDate.Value:MM/dd/yyyy}#");
                    }
                }
                FirstDT.DefaultView.RowFilter = string.Join(" AND ", filters);
                RowsCountTB.Text = FirstGrid.Items.Count.ToString();
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            foreach (UIElement el in FilterPanel.Children)
            {
                if (el is ComboBox cb) cb.SelectedIndex = 0;
                else if (el is DatePicker dp) dp.SelectedDate = null;
            }
            FirstDT.DefaultView.RowFilter = string.Empty;
            RowsCountTB.Text = FirstGrid.Items.Count.ToString();
        }

        private void LoadDemoTypes()
        {
            User = Environment.UserName;
            TypeTable = new DataSet();
            DataTable typeTable = new DataTable();
            typeTable.Columns.Add("id", typeof(int));
            typeTable.Columns.Add("type", typeof(string));
            typeTable.Columns.Add("include_dates", typeof(bool));
            typeTable.Columns.Add("upload_folder", typeof(string));
            typeTable.Columns.Add("process_all_flag", typeof(bool));
            typeTable.Columns.Add("import_flag", typeof(bool));
            typeTable.Rows.Add(1, "Demo Queue", false, "", false, false);
            TypeTable.Tables.Add(typeTable);
            TypeList.Add("Demo Queue");
            TypeDropdown.ItemsSource = TypeList;
        }

        private void LoadDemoFirstGrid()
        {
            FirstDT = new DataTable();
            FirstDT.Columns.Add("id", typeof(int));
            FirstDT.Columns.Add("button_Open", typeof(string));
            FirstDT.Columns.Add("Name", typeof(string));
            FirstDT.Columns.Add("Status", typeof(string));
            FirstDT.Columns.Add("Date", typeof(DateTime));
            FirstDT.Columns.Add("color", typeof(string));

            FirstDT.Rows.Add(1, "Open", "Alpha Project",    "Pending", new DateTime(2026, 3, 15), "");
            FirstDT.Rows.Add(2, "Open", "Beta Initiative",  "Active",  new DateTime(2026, 3, 20), "FFFF99");
            FirstDT.Rows.Add(3, "Open", "Gamma Task",       "Done",    new DateTime(2026, 3, 10), "99FF99");
            FirstDT.Rows.Add(4, "Open", "Delta Work",       "Pending", new DateTime(2026, 4,  1), "");
            FirstDT.Rows.Add(5, "Open", "Epsilon Item",     "Active",  new DateTime(2026, 3, 25), "FFCCCC");

            backupOne = FirstDT.Copy();
        }

        private void LoadDemoSecondGrid()
        {
            var demoRow = FirstDT.Rows.Cast<DataRow>()
                                      .FirstOrDefault(r => (int)r["id"] == ClickedRowID);
            if (demoRow == null) return;

            FieldCollection fields = new FieldCollection
            {
                Fields = new Field[]
                {
                    new Field { Name="name",     Label="Name",     DataType="string", Value=demoRow["Name"].ToString(),          Order=1, ReadOnly=false, ID=1, Color="" },
                    new Field { Name="status",   Label="Status",   DataType="enum",   Value=demoRow["Status"].ToString().ToLower(), Order=2, ReadOnly=false, ID=2, Color="" },
                    new Field { Name="date",     Label="Date",     DataType="date",   Value=demoRow["Date"].ToString(),           Order=3, ReadOnly=false, ID=3, Color="" },
                    new Field { Name="complete", Label="Complete", DataType="bool",   Value="false",                              Order=4, ReadOnly=false, ID=4, Color="" },
                }
            };
            EnumCollection enums = new EnumCollection
            {
                Enums = new Enum[]
                {
                    new Enum { Name="status", Value="pending", Label="Pending" },
                    new Enum { Name="status", Value="active",  Label="Active"  },
                    new Enum { Name="status", Value="done",    Label="Done"    },
                }
            };

            string detailsXml, enumsXml;
            var settings = new System.Xml.XmlWriterSettings { OmitXmlDeclaration = true };
            using (var sw = new StringWriter())
            using (var xw = System.Xml.XmlWriter.Create(sw, settings))
            {
                new XmlSerializer(typeof(FieldCollection)).Serialize(xw, fields);
                detailsXml = sw.ToString();
            }
            using (var sw = new StringWriter())
            using (var xw = System.Xml.XmlWriter.Create(sw, settings))
            {
                new XmlSerializer(typeof(EnumCollection)).Serialize(xw, enums);
                enumsXml = sw.ToString();
            }

            SecondDT = new DataTable();
            SecondDT.Columns.Add("details", typeof(string));
            SecondDT.Columns.Add("enums",   typeof(string));
            SecondDT.Rows.Add(detailsXml, enumsXml);
            backupTwo = SecondDT.Copy();
        }

    }

    static class Helper
    {
        public static byte[] ToByteArray(this string str)
        {
            return System.Text.Encoding.ASCII.GetBytes(str);
        }
    }
}
