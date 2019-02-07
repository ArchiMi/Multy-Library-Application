using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Diagnostics;
using System.Data;
using System.Collections.ObjectModel;
using System.IO;
using System.ComponentModel;
using Additional;

namespace MultyLibraryApplication
{
    public class InfoTransact
    {
        public string TransactNum { get; set; }
        public DateTime DataTime { get; set; }
        public string Result { get; set; }
        public string OperationType { get; set; }
        public string UnicNum { get; set; }
        public string Gate { get; set; }
        public string Status { get; set; }

        public void Destroy()
        {
            //GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Plugins plugins;
        private List<InfoTransact> transactCollection;

        public MainWindow()
        {
            try
            {
                InitializeComponent();

                transactCollection = new List<InfoTransact>();
                dataGrid.ItemsSource = transactCollection;

                plugins = new Plugins();

                Thread thraed = new Thread(Loading);
                thraed.IsBackground = true;
                thraed.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Loading()
        {
            try
            {
                LoadLibrary();

                Thread threadSqlConnection = new Thread(
                delegate ()
                {
                    try
                    {
                        UIUtils rotor = new UIUtils(dataGrid, plugins, transactCollection);

                        //while (true)
                        for (int i = 0; i < 1500; i++)
                        {
                            Cmd(rotor);

                            GC.Collect(3, GCCollectionMode.Forced);
                            GC.WaitForPendingFinalizers();
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(ex.Message);
                    }
                });
                threadSqlConnection.IsBackground = true;
                threadSqlConnection.Start();

                Thread.Sleep(10);
            }
            catch (Exception)
            {
                
            }
        }

        private void LoadLibrary()
        {
            plugins.Load();
        }

        private void Cmd(UIUtils utils)
        {
            try
            {
                utils.GetListTransact();
                Thread.Sleep(350);
            }
            catch (Exception)
            {

            }
        }
    }
}
