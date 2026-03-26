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
using Excel;//DataReader;
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
                                ";Connection Timeout=12000";
        }

        public static SqlConnection ConnectToDB()
        {
            var ConnectionString = AcquireConnectString();
            SqlConnection Connection = new SqlConnection(ConnectionString);
            Connection.Open();

            return Connection;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                DeselectedIndex = -1;
                SelectedIndex = -1;
                //GetMainWindow = this;
                FromTB.Visibility = Visibility.Collapsed;
                ToTB.Visibility = Visibility.Collapsed;
                FromDatePicker.Visibility = Visibility.Collapsed;
                ToDatePicker.Visibility = Visibility.Collapsed;
                Connection = ConnectToDB();
                TypeTable = new DataSet();
                SqlDataAdapter da = new SqlDataAdapter("Select * from dbo.q_Type", Connection);
                da.SelectCommand.CommandTimeout = 12000;
                SqlCommandBuilder cmdBuilder = new SqlCommandBuilder(da);

                da.Fill(TypeTable);

                User = Environment.UserName;
                DataGrid dt = new DataGrid();
                foreach (DataRow r in TypeTable.Tables[0].Rows)
                    TypeList.Add(r.ItemArray[1].ToString());
#if DEBUG
                TypeList.Add("--- SIMULATED ---");
#endif
                TypeDropdown.ItemsSource = TypeList;
                Connection.Close();
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

        private void GetFirstGridData()
        {
            try
            {
                FirstDT = new DataTable();
                Connection.Open();
                SqlCommand cmd = new SqlCommand("dbo.q_Load_List", Connection);
                cmd.CommandTimeout = 12000;
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@user", User));
                cmd.Parameters.Add(new SqlParameter("@type", TypeDropdown.Items[ActiveTypeIndex].ToString()));
                if (FromDatePicker.SelectedDate != null)
                    cmd.Parameters.Add(new SqlParameter("@date1", FromDatePicker.SelectedDate.ToString()));
                if (ToDatePicker.SelectedDate != null)
                    cmd.Parameters.Add(new SqlParameter("@date2", ToDatePicker.SelectedDate.ToString()));
                SqlDataReader rdr = cmd.ExecuteReader();
                FirstDT.Load(rdr);
                backupOne = FirstDT.Copy();
                Connection.Close();

                GenerateFirstGrid();
                
            }
            catch (Exception ex)
            {
                LogError(ex);
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
                //ClickedContents = row.ItemArray[FirstDT.Columns.Ordinal].ToString();
                //ClickedRowID = (sender as ExButton).ID;
                //ClickedContents = (sender as ExButton).Text;
                SecondDT = new DataTable();
                Connection.Open();
                SqlCommand cmd = new SqlCommand("dbo.q_Load_Details", Connection);
                cmd.CommandTimeout = 12000;
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@user", User));
                cmd.Parameters.Add(new SqlParameter("@type", TypeDropdown.Items[ActiveTypeIndex].ToString()));
                cmd.Parameters.Add(new SqlParameter("@id", ClickedRowID));
                cmd.Parameters.Add(new SqlParameter("@contents", ClickedContents));
                SqlDataReader rdr = cmd.ExecuteReader();
                SecondDT.Load(rdr);
                backupTwo = SecondDT.Copy();
                Connection.Close();
                //GenerateSecondGrid();
            }
            catch (Exception ex)
            {
                LogError(ex);
            }

        }

        private void DG_Button_Click(object sender, RoutedEventArgs e)
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
                SecondDT = new DataTable();
                Connection.Open();
                SqlCommand cmd = new SqlCommand("dbo.q_Load_Details", Connection);
                cmd.CommandTimeout = 12000;
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add(new SqlParameter("@user", User));
                cmd.Parameters.Add(new SqlParameter("@type", TypeDropdown.Items[ActiveTypeIndex].ToString()));
                cmd.Parameters.Add(new SqlParameter("@id", ClickedRowID));
                cmd.Parameters.Add(new SqlParameter("@contents", ClickedContents));
                SqlDataReader rdr = cmd.ExecuteReader();
                SecondDT.Load(rdr);
                backupTwo = SecondDT.Copy();
                Connection.Close();

                GenerateSecondGrid();
                
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
                        cmd.CommandTimeout = 12000;
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

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SecondDT == null)
                    return;
                List<Field> f = new List<Field>();
                Dictionary<string, string> dic = new Dictionary<string, string>();
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
                                    string rem = string.Empty;
                                    if (result != null && result.Success == true)
                                    {
                                        rem = v.Remove(0, result.Index);
                                        v = rem;
                                    }

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
                            field.Value = v != null ? v : "";
                            f.Add(field);
                        }

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
                        cmd.CommandTimeout = 12000;
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
                    MessageBox.Show(message);
                var refresh = ResponseDT.Rows[0][0].ToString();
                if (refresh.Equals("True"))
                    GetFirstGridData();
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

        private void FillButton_Click(object sender, RoutedEventArgs e)
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
            GetFirstGridData();
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
                            cmd.CommandTimeout = 12000;
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
    }

    static class Helper
    {
        public static byte[] ToByteArray(this string str)
        {
            return System.Text.Encoding.ASCII.GetBytes(str);
        }
    }
}
