using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
        private int ClickedRowID;
        private string ClickedContents;
        SqlConnection Connection;
        public MainWindow()
        {
            InitializeComponent();
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
                                ";Trusted_Connection=False";
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
            Connection = ConnectToDB();
            DataSet tableData = new DataSet();
            SqlDataAdapter da = new SqlDataAdapter("Select * from Queue.dbo.Type", Connection);
            SqlCommandBuilder cmdBuilder = new SqlCommandBuilder(da);
            da.Fill(tableData);

            User = Environment.UserName;
            DataGrid dt = new DataGrid();
            foreach (DataRow r in tableData.Tables[0].Rows)
                TypeList.Add(r.ItemArray[1].ToString());
            TypeDropdown.ItemsSource = TypeList;
        }

        private void TypeDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FieldsPanel.Children.Clear();
            GetFirstGridData();
        }

        private void GetFirstGridData()
        {
            FirstDT = new DataTable();
            SqlCommand cmd = new SqlCommand("Queue.dbo.q_Load_List", Connection);
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@user", User));
            cmd.Parameters.Add(new SqlParameter("@type", TypeDropdown.SelectedValue.ToString()));
            if (FromDatePicker.SelectedDate != null)
                cmd.Parameters.Add(new SqlParameter("@date1", FromDatePicker.SelectedDate.ToString()));
            if (ToDatePicker.SelectedDate != null)
                cmd.Parameters.Add(new SqlParameter("@date2", ToDatePicker.SelectedDate.ToString()));
            SqlDataReader rdr = cmd.ExecuteReader();
            FirstDT.Load(rdr);
            backupOne = FirstDT.Copy();
            GenerateFirstGrid();
        }

        private void GenerateFirstGrid(bool regenerating = false)
        {
            if (regenerating)
                FirstDT = backupOne.Copy();
            FirstGrid.ItemsSource = null;
            FirstGrid.Columns.Clear();
            FirstGrid.Items.Clear();
            FirstGrid.Items.Refresh();
            List<string> colsToKeep = new List<string>();
            for (int i = 0; i < FirstDT.Columns.Count; i++)
            {
                if (FirstDT.Columns[i].ColumnName.StartsWith("button_"))
                {
                    var label = FirstDT.Columns[i].ColumnName.Replace("button_", "");
                    DataGridTemplateColumn col1 = new DataGridTemplateColumn();
                    col1.Header = label;
                    FrameworkElementFactory factory1 = new FrameworkElementFactory(typeof(ExButton));
                    Binding b = new Binding(FirstDT.Columns[i].ColumnName);
                    factory1.SetValue(ExButton.ContentProperty, label);
                    factory1.SetValue(ExButton.TextProperty, b);
                    factory1.SetValue(ExButton.IDProperty, b);
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
                    factory1.SetValue(ExButton.StyleProperty, style);
                    cellTemplate1.Triggers.Add(trig);
                    col1.CellTemplate = cellTemplate1;
                    cellTemplate1.VisualTree = factory1;
                    FirstGrid.Columns.Add(col1);
                    continue;
                }
                if (FirstDT.Columns[i].ColumnName.StartsWith("document_"))
                {
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
                    dgc.Header = FirstDT.Columns[i].ColumnName;
                    dgc.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);

                    FrameworkElementFactory sb = new FrameworkElementFactory(typeof(StackPanel));
                    sb.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
                    sb.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
                    sb.AppendChild(tb2Factory);
                    //sb.AppendChild(browseButtonFactory);
                    DataTemplate dataTemplate = new DataTemplate { VisualTree = sb };
                    //dataTemplate.Triggers.Add(trig);
                    dgc.CellTemplate = dataTemplate;
                    FirstGrid.Columns.Add(dgc);
                    continue;
                }
                if (FirstDT.Columns[i].ColumnName == "id")
                {
                    DataGridTextColumn col1 = new DataGridTextColumn();
                    col1.Visibility = Visibility.Hidden;
                    FirstGrid.Columns.Add(col1);
                    continue;
                }
                if (FirstDT.Columns[i].DataType.Equals(typeof(Boolean)))
                {
                    DataGridCheckBoxColumn col1 = new DataGridCheckBoxColumn();
                    col1.Header = FirstDT.Columns[i].ColumnName;
                    col1.Binding = new Binding(FirstDT.Columns[i].ColumnName);
                    col1.IsReadOnly = true;
                    FirstGrid.Columns.Add(col1);
                    continue;
                }
                else
                {
                    DataGridTextColumn col1 = new DataGridTextColumn();
                    col1.Header = FirstDT.Columns[i].ColumnName;
                    col1.Binding = new Binding(FirstDT.Columns[i].ColumnName);
                    col1.IsReadOnly = true;
                    FirstGrid.Columns.Add(col1);
                    continue;
                }

            }
            FirstGrid.ItemsSource = FirstDT.DefaultView;
        }

        private void DG_Button_Click(object sender, RoutedEventArgs e)
        {
            ClickedRowID = (sender as ExButton).ID;
            ClickedContents = (sender as ExButton).Text;
            SecondDT = new DataTable();
            SqlCommand cmd = new SqlCommand("Queue.dbo.q_Load_Details", Connection);
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.Parameters.Add(new SqlParameter("@user", User));
            cmd.Parameters.Add(new SqlParameter("@type", TypeDropdown.SelectedValue));
            cmd.Parameters.Add(new SqlParameter("@id", ClickedRowID));
            cmd.Parameters.Add(new SqlParameter("@contents", ClickedContents));
            SqlDataReader rdr = cmd.ExecuteReader();
            SecondDT.Load(rdr);
            backupTwo = SecondDT.Copy();
            GenerateSecondGrid();
            
        }

        private void GenerateSecondGrid(bool regenerating = false)
        {
            if (SecondDT == null)
                return;
            if (regenerating)
                SecondDT = backupTwo.Copy();
            FieldsPanel.Children.Clear();

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

            var xml = SecondDT.Rows[0].Field<string>("details").ToString().ToByteArray();
            MemoryStream stream = new MemoryStream(xml);
            using (TextReader reader = new StreamReader(stream))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(FieldCollection));
                var stuff = (FieldCollection)serializer.Deserialize(reader);
                if (stuff.Fields.Count() > 0)
                    FieldsPanel.Visibility = Visibility.Visible;
                int i = 1;
                var newList = stuff.Fields.ToList().OrderBy(f => f.Order).ToList();
                newList.ForEach(s => OrderedFields.Add(s));
                foreach (Field f in newList)
                {
                    if (string.IsNullOrEmpty(f.Label))
                        continue;
                    FieldsPanel.RowDefinitions.Add(new RowDefinition());

                    Border b = new Border()
                    {
                        BorderBrush = Brushes.Black,
                        BorderThickness = new Thickness(1, 1, 1, 1),
                        HorizontalAlignment = HorizontalAlignment.Stretch
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
                        if (!f.ReadOnly)
                            child = new ComboBox
                            {
                                ItemsSource = f.Enums,
                                DisplayMemberPath = "Label",
                                SelectedItem = f.Enums.Where(en => en.Value.ToString().Equals(f.Value)).FirstOrDefault(),
                                IsHitTestVisible = !f.ReadOnly,
                            };
                        else
                            child = new TextBlock { Text = f.Enums.Where(en => en.Value.ToString().Equals(f.Value)).Select(p => p.Label).FirstOrDefault(), Margin = new Thickness(3, 0, 5, 0), Padding = new Thickness(5, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
                    }
                    

                    else if (f.DataType.ToLower().Equals("path"))
                        child = GetLinkCell(f);

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
                    i++;
                }
            }
        }

        private StackPanel GetLinkCell(Field f)
        {
            if (string.IsNullOrEmpty(f.Value))
                return new StackPanel { };
            TextBlock textBlock = new TextBlock();
            textBlock.SetValue(TextBlock.TextProperty, f.Value);
            StackPanel sp = new StackPanel();
            sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            Hyperlink hl = new Hyperlink();
            Uri uri = new Uri(f.Value);
            hl.SetValue(Hyperlink.NavigateUriProperty, uri);
            hl.Inlines.Add(textBlock);
            hl.AddHandler(Hyperlink.ClickEvent, (RoutedEventHandler)DG_Hyperlink_Click);
            TextBlock tb = new TextBlock();
            tb.Inlines.Add(hl);
            tb.Margin = new Thickness(5, 0, 10, 0);
            tb.VerticalAlignment = VerticalAlignment.Center;
            Button b = new Button();
            b.HorizontalAlignment = HorizontalAlignment.Right;
            b.Content = "Browse...";
            b.AddHandler(Button.ClickEvent, (RoutedEventHandler)DG_Browse_Click);
            if (!f.ReadOnly)
                sp.Children.Add(b);
            sp.Children.Add(tb);
            
            return sp;
        }

        private void DG_Browse_Click(object sender, RoutedEventArgs e)
        {
            var s = ((sender as Button).Parent as StackPanel).Children.OfType<TextBlock>().FirstOrDefault();
            OpenFileDialog dialog = new OpenFileDialog();
            var result = dialog.ShowDialog();
            if ((bool)result) 
            {
                s.Inlines.Clear();
                Hyperlink hl = new Hyperlink();
                Uri uri = new Uri(dialog.FileName);
                hl.SetValue(Hyperlink.NavigateUriProperty, uri);
                hl.Inlines.Add(dialog.FileName);
                hl.AddHandler(Hyperlink.ClickEvent, (RoutedEventHandler)DG_Hyperlink_Click);
                s.Inlines.Add(hl);
            }
        }

        private void DG_Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)e.OriginalSource;
            Process.Start(link.NavigateUri.LocalPath);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
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
                            v = (chi.GetValue(dp) as Enum).Value.ToString();
                        }
                        else if (chi.GetType().Name.Equals(typeof(CheckBox).Name))
                        {
                            dp = CheckBox.IsCheckedProperty;
                            v = chi.GetValue(dp).ToString();
                        }
                        else if (chi.GetType().Name.Equals(typeof(StackPanel).Name))
                        {
                            var hl = (chi as StackPanel).Children.OfType<TextBlock>().FirstOrDefault();
                            if (hl == null)
                                continue;
                            v = (hl.Inlines.FirstInline as Hyperlink).NavigateUri.ToString().Replace("file:///", "");
                        }
                        else
                        {
                            var fieldtest = OrderedFields.ElementAt<Field>(r - 1);
                            if (!fieldtest.ReadOnly)
                                dp = TextBox.TextProperty;
                            v = chi.GetValue(dp).ToString();
                        }

                        var field = OrderedFields.ElementAt<Field>(r - 1);
                        field.Value = v;
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
                    SqlCommand cmd = new SqlCommand("Queue.dbo.q_Save_Details", Connection);
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.Add(new SqlParameter("@user", User));
                    cmd.Parameters.Add(new SqlParameter("@type", TypeDropdown.SelectedValue));
                    cmd.Parameters.Add(new SqlParameter("@id", ClickedRowID));
                    cmd.Parameters.Add(new SqlParameter("@contents", ClickedContents));
                    cmd.Parameters.Add(new SqlParameter("@xml", x));
                    SqlDataReader rdr = cmd.ExecuteReader();
                    ResponseDT.Load(rdr);

                }

            }
            var message = ResponseDT.Rows[0][1].ToString();
            if (!string.IsNullOrEmpty(message))
                MessageBox.Show(message);
            var refresh = ResponseDT.Rows[0][0].ToString();
            if (refresh.Equals("True"))
                GetFirstGridData();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if(SecondDT != null)
            {
                GenerateSecondGrid(regenerating: true);
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
