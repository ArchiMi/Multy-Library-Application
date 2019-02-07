using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Reflection;
using Additional;

namespace MultyLibraryApplication
{
    public class UIUtils : IDisposable
    {
        private DataGrid dataGrid;
        private Plugins plugins;
        private List<InfoTransact> transactCollection;
        //private string newTransact = DateTime.Now.ToString("yyyyMMddhhmmssfff");
        private string hows;

        public UIUtils(DataGrid dataGrid, Plugins plugins, List<InfoTransact> transactCollection)
        {
            this.dataGrid = dataGrid;
            this.plugins = plugins;
            this.transactCollection = transactCollection;            
        }

        public void GetListTransact()
        {
            try
            {
                this.hows = GetListHows();
                Dictionary<string, string> transaction = GetTransaction();
                foreach (KeyValuePair<string, string> transact in transaction)
                {
                    Thread.Sleep(50);
                    ProcessingTransactionAsync(transact);
                }
            }
            catch (Exception)
            {
                Thread.Sleep(180000);
            }
        }

        private Boolean IsExistsHowPoint(string howPoint)
        {
            string pathGatesParams = $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}\\Temp\\{howPoint}";
            return Directory.Exists(pathGatesParams);
        }

        private string GetListHows()
        {
            string hows = "";
            foreach (IPlugin plugin in plugins.Gateways)
            {
                if (plugin.HowCode.Length > 0)
                {
                    if (!hows.Contains(plugin.HowCode))
                        hows = hows.Length > 0 ? $"{hows},{plugin.HowCode}" : $"{plugin.HowCode}";
                    else
                        throw new Exception($"Обнаружены дубликаты библиотек шлюзов. {plugin.HowCode}");
                }
            }
            return hows;
        }

        private OperationResult ProcessingTransaction(string transact, string cmd)
        {
            //TEST
            //return new OperationResult(transact, cmd, "9000");

            using (OperationValues trava = GetTransactionValues(transact, cmd))
            {

                IPlugin gateway = null;
                gateway = (from plugin in plugins.Gateways where plugin.HowCode == trava.HowPoint select plugin).First().CreateNewInstance();

                try
                {
                    try
                    {
                        gateway.StartOperation(cmd, transact);
                        return new OperationResult(transact, cmd, trava.HowPoint);
                    }
                    catch (Exception)
                    {
                        return new OperationResult(transact, cmd, trava.HowPoint);
                    }
                }
                finally
                {
                    if (gateway != null)
                    {
                        gateway.Destroy();
                        gateway = null;
                    }
                }
            }
        }
            
        private void UpdateTransactNewSchem(OperationResult transactionResult)
        {
            this.EditInGui(transactionResult);
        }
                
        private Dictionary<string, string> GetTransaction()
        {
            string transact_num = this.NewTransact();

            Dictionary<string, string> dict = new Dictionary<string, string>();

            //TEST DATA
            string cmd = "";
            Random r = new Random();
            switch (r.Next(2))
            {
                case 0:
                    {
                        cmd = "cmd_one"; 
                        break;
                    }
                case 1:
                    {
                        cmd = "cmd_two";
                        break;
                    }
                case 2:
                    {
                        cmd = "Verify";
                        break;
                    }
            }
            

            dict.Add(transact_num, cmd);
            return dict;
        }

        private void UpdateTransactionToWait(string transact)
        {
            
        }

        private async void ProcessingTransactionAsync(KeyValuePair<string, string> transact)
        {
            try
            {
                this.AddInGrid(transact);
                //LogWrite.WriteInfo($"{transact.Key}_{transact.Value}", $"----------------------> 1");
                //Здесь какой-то тормоз, порой очень серьезный.
                using (OperationResult trnRes = await Task<OperationResult>.Factory.StartNew((Func<OperationResult>)(() =>
                {
                    Thread.Sleep(50);
                    //LogWrite.WriteInfo($"{transact.Key}_{transact.Value}", $"----------------------> 2");
                    return this.ProcessingTransaction(transact.Key, transact.Value);
                })))
                {
                    this.UpdateTransactNewSchem(trnRes);
                }                    
            }
            catch (Exception)
            {

            }
        }

        private void AddInGrid(KeyValuePair<string, string> transact)
        {
            try
            {
                InfoTransact item = new InfoTransact()
                {
                    TransactNum = transact.Key,
                    DataTime = DateTime.Now,
                    Result = "",
                    OperationType = transact.Value,
                    UnicNum = "",
                    Gate = "",
                    Status = "Wait"
                };

                dataGrid.Dispatcher.Invoke((Action)(() =>
                {
                    //transactCollection.Insert(transactCollection.Count, it);
                    transactCollection.Insert(0, item);
                    item.Destroy();

                    dataGrid.Items.Refresh();
                }));
            }
            catch (OutOfMemoryException)
            {
                //error handling
            }
            catch (Exception)
            {
                //error handling
            }
        }

        private void EditInGui(OperationResult trnRes)
        {
            try
            {
                dataGrid.Dispatcher.Invoke((Action)(() =>
                {
                    Random r = new Random();
                    string status = r.Next(2).Equals(0) ? "Ok" : "Error";

                    if (transactCollection != null)
                    {
                        if (transactCollection.Count > 0)
                        {
                            string transact = trnRes.Transact;
                            string cmd = trnRes.Command.ToString();

                            transactCollection.Where(x =>
                                x.TransactNum.Equals(transact) &
                                x.OperationType == cmd)
                                    .ToList().ForEach(y =>
                                    {
                                        y.TransactNum = trnRes.Transact != null ? trnRes.Transact : "";
                                        y.DataTime = y.DataTime;
                                        y.OperationType = trnRes.Command.ToString();
                                        y.Result = $"Test operation result { (status.Equals("Ok") ? "Good" : "Bad") }";
                                        y.UnicNum = $"{ (status.Equals("Ok") ? "Unic operation number" : "") }";
                                        y.Gate = trnRes.HowPoint;
                                        y.Status = status;
                                    });

                            dataGrid.Items.Refresh();
                        }
                    }
                }));
            }
            catch (OutOfMemoryException)
            {

            }
            catch (Exception)
            {

            }
        }

        private string NewTransact()
        {
            return DateTime.Now.ToString("yyyyMMddhhmmssfff");
        }

        private OperationValues GetTransactionValues(string transact, string cmd /*, ref string requestFields*/)
        {
            try
            {
                OperationValues trava = new OperationValues();
                string commandText = $"SELECT fields, how, how_point, id, fields_history, try, next_try_date  FROM exchange WHERE transact = '{transact}'";

                //TEST DATA
                Random rnd = new Random();

                Dictionary<string, string> fields = new Dictionary<string, string>();
                fields.Add("field_1", "val 1");
                fields.Add("field_2", "val 2");
                fields.Add("field_3", "val 3");
                fields.Add("field_4", "val 4");

                string how_point = rnd.Next(0, 2) == 1 ? "9000" : "9500";
                //END TEST

                trava.Transact = this.NewTransact();
                trava.Command = cmd;
                trava.Fields = fields; 
                trava.How = "GateOne";
                trava.HowPoint = how_point;
                trava.Id = "1221231321312321";
                trava.FieldsHistory = "dsfdsfskdfnlkdskmfdskmgsdsd";
                trava.ResultText = fields;
                trava.TryCount = "0";
                trava.NextTryDate = DateTime.Now;

                return trava;
            }
            catch (Exception)
            {
                return null;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {

            }
        }

        public void Dispose()
        {
            Dispose(true);
            //GC.SuppressFinalize(this);
        }
    }
}
