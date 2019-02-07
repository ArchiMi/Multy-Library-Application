using System;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using System.Xml.Serialization;
using System.IO;
using UnistreamConsoleApplication.ServiceReference1;
using System.Diagnostics;


namespace UnistreamConsoleApplication
{
    class MyLog
    {
        private const string logPath = "C:\\Users\\ilya\\work\\unistream\\logs\\";
        public void WriteCmd(string mess, string cmd)
        {
            var fileName = logPath + cmd + "log.txt";
            FileInfo aFile = new FileInfo(fileName);
            if (aFile.Exists == false) { aFile.Create(); /*Thread.Sleep(1000);*/ }
            FileStream fs = new FileStream(fileName, FileMode.Append, FileAccess.Write);
            StreamWriter sw = new StreamWriter(fs);
            sw.WriteLine(cmd);
            sw.WriteLine(mess);
            sw.Close();
        }
    }


    class Program
    {
        private const string mysqlConnectString = "Database=deltakey;Data Source=deltakey.net;User Id=exchange;Password=yj7h4MyR2YUh933x;charset=cp1251";
        static MySqlConnection myConnection = new MySqlConnection(mysqlConnectString);
        static MyLog log = new MyLog();
        private const int senderID = 294278;

        static void Main()
        {
            Console.WriteLine("Внимание! Запущен тестовый шлюз! Продолжить?(y/n)");
            var x = Console.Read();
            if(x != 121)
            {
                return;
            }            
            //myConnection = new MySqlConnection(mysqlConnectString);
            myConnection.Open();

            while (true)
            {
                try
                {
                    if(myConnection.State != System.Data.ConnectionState.Open)
                        myConnection.Open();
                    Dictionary<String, String> transferParams = GetTranferParamsDeltakey();
                    if (transferParams.Count == 0 || (transferParams.Count != 39 && transferParams.Count != 30 && transferParams.Count != 32 && transferParams.Count != 14 && transferParams.Count != 15 && transferParams.Count != 8 && transferParams.Count != 7))
                    {
                        Thread.Sleep(1000);
                        Console.WriteLine("sleep {0}", DateTime.Now.ToString());
                        continue;
                    }
                    switch (transferParams["nextOperation"])
                    {
                        case "check":
                            CheckTransferPosibility(transferParams);
                            break;
                        case "create_transfer":
                            CheckCreateTransferPosibility(transferParams);
                            break;
                        case "send_transfer":
                            SendTransfer(transferParams);
                            break;
                        case "check_payout":
                            CheckPayoutPosibility(transferParams);
                            break;
                        case "estimate_main_amount":
                           if (Convert.ToSingle(transferParams["sumIn"]) > 0)
                                EstimateMainAmount(transferParams);
                            else
                            {
                                EstimateAmountPrepareTransfer(transferParams);
                            }
                            break;
                        case "find_person":
                            FindPerson(transferParams);
                            break;
                        case "check_and_die":
                            CheckTransferPosibility(transferParams);
                            break;
                        case "pay":
                            PayTransfer(transferParams);
                            break;
                        case "cancel":
                            CancelTransfer(transferParams);
                            break;
                        case "cancel_wait":
                            GetCancelTransferAnswer(transferParams);
                            break;
                        case "status":
                            GetTransferBySourceID(transferParams);
                            break;
                        case "return":
                            ReturnTransfer(transferParams);
                            break;
                        case "payout":
                            PayoutTransfer(transferParams);
                            break;
                        default:
                            break;
                    }

                    Thread.Sleep(1000);
                }
                catch (Exception e)
                {

                    log.WriteCmd(e.Message, "CatchExceptions");
                    Console.WriteLine(e.Message);
                    //throw e;
                    //Console.WriteLine(e.StackTrace);
                    Thread.Sleep(1000);
                    myConnection.Close();
                }
                /*finally
                {
                    Thread.Sleep(1000);
                }*/
            }
            //myConnection.Close();
        }

        private static void EstimateAmountPrepareTransfer(Dictionary<string, string> transferParams)
        {
            WebServiceClient unistream = new WebServiceClient();
            string next_operation = "";
            string next_operation_final = "";
            string resultText = "";
            string status = "2";
            string result = "0";
            string query = "";
            try
            {                
                double recieverSum = Convert.ToDouble(transferParams["sumOut"]);
                int country = Convert.ToInt32(transferParams["countryPayout"]);
                var sourceID = Convert.ToInt64(transferParams["transact"]);
                next_operation = transferParams["nextOperation"];
                //int senderID = 294278;
                int senderCurr = GetUnistreamCurr(Convert.ToInt32(transferParams["sumInCurr"]));
                int recieverCurr = GetUnistreamCurr(Convert.ToInt32(transferParams["sumOutCurr"]));
                //по коду страны получаем участника перевода - банк получатель
                var participatorRecieverBank = GetVirtBankByCountry(country);
                //подготавливаем перевод
                Transfer transfer = PrepareTransferLite(recieverSum, senderCurr, recieverCurr, senderID, participatorRecieverBank);
                if (transfer == null)
                {
                    throw new Exception("tranfer impossible EstimateAmountPrepareTransfer");
                }                
                transfer.Amounts.ToList().ForEach(
                    a =>
                    Console.WriteLine(
                        @"a.CurrencyID={0}, a.Type={1}, a.Sum={2}",
                        a.CurrencyID, a.Type, a.Sum));
                Console.WriteLine("Комиссии:");
                // выведем комиссии
                transfer.Services.ToList().ForEach(
                    s =>
                    Console.WriteLine(
                        @"s.Fee={0}, s.CurrencyID={1}, s.ParticipatorID={2}, s.Mode={3}, s.ServiceID={4}", s.Fee,
                        s.CurrencyID, s.ParticipatorID, s.Mode, s.ServiceID));
                double feeSum = 0;
                transfer.Services.ToList().ForEach(
                        s => feeSum += s.Fee
                    );
                Console.WriteLine("Common fee {0}", feeSum);
                var sumIn = "";
                transfer.Amounts.ToList().ForEach(
                        c => sumIn += GetMainSum(c)
                        );
                sumIn = (Convert.ToSingle(sumIn) + feeSum).ToString();
                resultText = feeSum.ToString() + "&" + transferParams["sumInCurr"] + "&" + sumIn.ToString() + "&" + transferParams["sumInCurr"] + "&";
                var rate = "";
                if (recieverCurr != senderCurr)
                {
                    transfer.Amounts.ToList().ForEach(
                        c => resultText += GetEstimatedPayoutSum(c)
                        );
                    transfer.Amounts.ToList().ForEach(
                        c => rate += GetRate(c)
                        );
                }
                else
                {
                    transfer.Amounts.ToList().ForEach(
                        c => resultText += GetMainSum(c)
                        );
                }
                resultText += "&" + transferParams["sumOutCurr"];
                resultText += "&" + rate;
                Console.WriteLine(resultText);
                resultText = resultText.Replace(',', '.');
                next_operation_final = "kernel_" + next_operation + "_ok";
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                Console.WriteLine(e.Message);
                resultText = e.Message.ToString();
                next_operation_final = "kernel_" + next_operation + "_err";
                result = "1";
            }
            finally
            {
                query = "UPDATE `deltakey`.`exchange` SET `last_operation` = '" + next_operation + "', `next_operation` = '" + next_operation_final + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "', next_try_date = NOW()+INTERVAL 2 SECOND WHERE `exchange`.`transact` = " + transferParams["transact"] + ";";
                ExecQuery(query);
            }
        }

        private static void SendTransfer(Dictionary<string, string> transferParams)
        {
            WebServiceClient unistream = new WebServiceClient();
            string next_operation = "";
            string next_operation_final = "";
            string resultText = "";
            string status = "2";
            string result = "0";
            string query = "";
            try
            {
                //проверить существует перевод с таким source id ?       
                resultText = _GetTransferStatusBySourceID(transferParams["transact"]);
                if (!resultText.Equals("TransferNotFound"))
                    throw new Exception(resultText);
                //проверить параметры
                //CheckTransferInParams(transferParams);
                //CheckUnistreamServer();
                //ещё что-нибудь

                //
                string senderFirstName = transferParams["senderFirstName"];
                string senderMiddleName = transferParams["senderMiddleName"];
                string senderLastName = transferParams["senderLastName"];
                string recieverFirstName = transferParams["recieverFirstName"];
                string recieverMiddleName = transferParams["recieverMiddleName"];
                string recieverLastName = transferParams["recieverLastName"];
                double senderSum = Convert.ToDouble(transferParams["sumIn"]);
                double recieverSum = Convert.ToDouble(transferParams["sumOut"]);
                int country = Convert.ToInt32(transferParams["country"]);
                var sourceID = Convert.ToInt64(transferParams["transact"]);
                next_operation = transferParams["nextOperation"];
                //int senderID = 294278;
                string controlNumber = transferParams["controlNumber"];
                int senderCurr = GetUnistreamCurr(Convert.ToInt32(transferParams["senderCurr"]));
                int recieverCurr = GetUnistreamCurr(Convert.ToInt32(transferParams["recieverCurr"]));

                //создаем клиентов перевода
                var senderConsumer = new Person(); 
                //Console.WriteLine("Номер телефона отправителя = {0}", transferParams["phoneNumber"]);

                if (!FindPerson(senderFirstName, senderMiddleName, senderLastName, transferParams["phoneNumber"], out senderConsumer))
                    senderConsumer = CreatePersonFull(transferParams);

                // FindPerson(senderFirstName, senderMiddleName, senderLastName, transferParams["phoneNumber"], out senderConsumer);
                var recieverConsumer = CreatePerson(recieverFirstName, recieverMiddleName, recieverLastName);

                //по коду страны получаем участника перевода - банк получатель
                var participatorRecieverBank = GetVirtBankByCountry(country);

                //подготавливаем перевод
                //Transfer transfer = EstimateMainAmount(senderSum, senderCurr, recieverCurr, senderID, participatorRecieverBank);
                // Transfer transfer = new Transfer();
                var errorTextPrepareTransfer = "";
                Transfer transfer = PrepareTransfer(senderConsumer, recieverConsumer, recieverSum, senderCurr, recieverCurr, senderID, participatorRecieverBank, controlNumber, out errorTextPrepareTransfer);
                if (transfer == null)
                {
                    throw new Exception(errorTextPrepareTransfer);
                }
                /*
                var amounts = transfer.Amounts.ToList();
                double paySum = 0;
                double paySum2 = 0;
                double fee = 0;
                for (int i = 0; i < transfer.Amounts.Length; i++)
                {
                    if (transfer.Amounts[i].Type == AmountType.EstimatedPaidout)
                        paySum = transfer.Amounts[i].Sum;
                    if (transfer.Amounts[i].Type == AmountType.PrimaryPaidComission)
                        fee = transfer.Amounts[i].Sum;
                    if (transfer.Amounts[i].Type == AmountType.ActualPaid)
                        paySum2 = transfer.Amounts[i].Sum;
                    //if (transfer.Amounts[i].Type == AmountType.ActualPaidout)
                    //paySum2 = transfer.Amounts[i].Sum;
                }

                if (Double.Equals(paySum, (double)0))
                    resultText = fee.ToString() + '&' + paySum2.ToString();
                else
                    resultText = fee.ToString() + '&' + paySum.ToString();
                Console.WriteLine(resultText);
                */
                ArrayList iTR = InsertTransfer(transfer, sourceID);
                Console.WriteLine("Номер перевода - {0}, контрольный номер - {1}, статус - {2}, номер в системе отправителя - {3}", iTR[0].ToString(), iTR[1].ToString(), iTR[2].ToString(), iTR[3].ToString());
                resultText = iTR[0].ToString() + "&" + iTR[1].ToString() + "&" + iTR[2].ToString() + "&" + iTR[3].ToString();
                next_operation_final = "kernel_" + next_operation + "_ok";
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                Console.WriteLine(e.Message);
                //Console.WriteLine(e.Source);
                resultText = e.Message.ToString();
                next_operation_final = "kernel_" + next_operation + "_err";
                result = "1";
            }
            finally
            {
                query = "UPDATE `deltakey`.`exchange` SET `last_operation` = 'check', `next_operation` = '" + next_operation_final + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "', next_try_date = NOW()+INTERVAL 2 SECOND WHERE `exchange`.`transact` = " + transferParams["transact"] + ";";
                ExecQuery(query);
            }
        }

        private static void CheckCreateTransferPosibility(Dictionary<string, string> transferParams)
        {
            WebServiceClient unistream = new WebServiceClient();
            string next_operation = "";
            string next_operation_final = "";
            string resultText = "";
            string status = "2";
            string result = "0";
            string query = "";
            try
            {
                //проверить существует перевод с таким source id ?       
                resultText = _GetTransferStatusBySourceID(transferParams["transact"]);
                if (!resultText.Equals("TransferNotFound"))
                    throw new Exception(resultText);
                //проверить параметры
                //CheckTransferInParams(transferParams);
                //CheckUnistreamServer();
                //ещё что-нибудь

                //
                string senderFirstName = transferParams["senderFirstName"];
                string senderMiddleName = transferParams["senderMiddleName"];
                string senderLastName = transferParams["senderLastName"];
                string recieverFirstName = transferParams["recieverFirstName"];
                string recieverMiddleName = transferParams["recieverMiddleName"];
                string recieverLastName = transferParams["recieverLastName"];
                string recieverIsResident = transferParams["recieverIsResident"];
                double senderSum = Convert.ToDouble(transferParams["sumIn"]);
                double recieverSum = Convert.ToDouble(transferParams["sumOut"]);
                int country = Convert.ToInt32(transferParams["country"]);
                var sourceID = Convert.ToInt64(transferParams["transact"]);
                next_operation = transferParams["nextOperation"];
                //int senderID = 294278;
                string controlNumber = transferParams["controlNumber"];
                int senderCurr = GetUnistreamCurr(Convert.ToInt32(transferParams["senderCurr"]));
                int recieverCurr = GetUnistreamCurr(Convert.ToInt32(transferParams["recieverCurr"]));

                

                //создаем клиентов перевода
                var senderConsumer = new Person();
                //if (!FindPerson(senderFirstName, senderMiddleName, senderLastName, transferParams["phoneNumber"], out senderConsumer))
                    senderConsumer = CreatePersonFull(transferParams);

                    var recieverConsumer = CreatePerson(recieverFirstName, recieverMiddleName, recieverLastName, Convert.ToBoolean(Convert.ToInt32(recieverIsResident)), transferParams["country"]);

                //по коду страны получаем участника перевода - банк получатель
                var participatorRecieverBank = GetVirtBankByCountry(country);
                var errorTextPrepareTransfer = "";
                
                //подготавливаем перевод
                Transfer transfer = null;
                if (recieverSum <= 0)
                {
                    if (senderSum > 0)
                    {
                        transfer = EstimateMainAmount(senderSum, senderCurr, recieverCurr, senderID, participatorRecieverBank);
                        transfer = PrepareTransfer(senderConsumer, recieverConsumer, senderSum, senderCurr, recieverCurr, senderID, participatorRecieverBank, controlNumber, transfer, out errorTextPrepareTransfer);
                    }
                    else
                    {
                        throw new Exception("Суммы указаны неверно");
                    }
                }
                else
                {
                    transfer = PrepareTransfer(senderConsumer, recieverConsumer, recieverSum, senderCurr, recieverCurr, senderID, participatorRecieverBank, controlNumber, out errorTextPrepareTransfer);
                }
                if (transfer == null)
                {
                    throw new Exception(errorTextPrepareTransfer);
                }

                
                double feeSum = 0;
                transfer.Services.ToList().ForEach(
                        s => feeSum += s.Fee
                    );
                Console.WriteLine("Common fee {0}", feeSum);
                var sumIn = "";
                transfer.Amounts.ToList().ForEach(
                        c => sumIn += GetMainSum(c)
                        );
                sumIn = (Convert.ToSingle(sumIn) + feeSum).ToString();
                
                var payout_sum = "";
                if (recieverCurr != senderCurr)
                {
                    transfer.Amounts.ToList().ForEach(
                        c => payout_sum += GetEstimatedPayoutSum(c)
                        );              
                }
                else
                {
                    transfer.Amounts.ToList().ForEach(
                        c => payout_sum += GetMainSum(c)
                        );
                }

                resultText = transferParams["country"] + '&';
                resultText += (feeSum.ToString()).Replace(',', '.') + '&';
                resultText += transferParams["senderCurr"] + '&';
                resultText += (sumIn).Replace(',', '.') + '&';
                resultText += transferParams["senderCurr"] + '&';
                resultText += (payout_sum).Replace(',', '.') + '&';
                resultText += transferParams["recieverCurr"] + '&';
                resultText += transfer.ControlNumber + '&';
                //sender
                resultText += senderConsumer.LastName + '&';
                resultText += senderConsumer.FirstName + '&';
                resultText += senderConsumer.MiddleName + '&';
                if (Convert.ToSingle(payout_sum) > 15000)
                {
                    if (senderConsumer.Residentships.Length > 0)
                        resultText += (Convert.ToInt32(senderConsumer.Residentships[0].IsResident)).ToString() + '&';
                    else
                        resultText += "1&";
                    resultText += (senderConsumer.Documents[0].TypeID).ToString() + '&';
                    resultText += senderConsumer.Documents[0].Series + '&';
                    resultText += senderConsumer.Documents[0].Number + '&';
                    resultText += senderConsumer.Documents[0].IssueDate.ToString("yyyy-MM-dd") + '&';
                    resultText += senderConsumer.Documents[0].Issuer + '&';
                    resultText += senderConsumer.Documents[0].IssuerCode + '&';
                }
                else 
                {
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                }
                resultText += senderConsumer.Phones[0].AreaCode + senderConsumer.Phones[0].Number + '&';


                if (Convert.ToSingle(payout_sum) > 15000)
                {
                    resultText += senderConsumer.BirthDate.ToString("yyyy-MM-dd") + '&';
                    resultText += senderConsumer.BirthPlace + '&';

                    resultText += GetCoutryISOByCountryID(senderConsumer.Address.CountryID) + '&';
                    resultText += '&';//region
                    resultText += senderConsumer.Address.City + '&';
                    resultText += senderConsumer.Address.Street + '&';
                    resultText += senderConsumer.Address.House + '&';
                    resultText += senderConsumer.Address.Building + '&';
                    resultText += senderConsumer.Address.Flat + '&';
                    resultText += senderConsumer.Address.PostalCode + '&';
                }
                else 
                {
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';                    
                }
                //recipient
                resultText += recieverConsumer.LastName + '&';
                resultText += recieverConsumer.FirstName + '&';
                resultText += recieverConsumer.MiddleName + '&';
                resultText += (Convert.ToInt32(recieverConsumer.Residentships[0].IsResident)).ToString() + '&';

                if (senderConsumer.Documents.Length != 2)
                {
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                    resultText += '&';
                }
                else 
                {
                    resultText += (senderConsumer.Documents[1].TypeID).ToString() + '&';
                    resultText += senderConsumer.Documents[1].Series + '&';
                    resultText += senderConsumer.Documents[1].Number + '&';
                    resultText += senderConsumer.Documents[1].IssueDate.ToString("yyyy-MM-dd") + '&';
                    resultText += senderConsumer.Documents[1].Issuer + '&';
                    resultText += senderConsumer.Documents[1].IssuerCode + '&';
                    resultText += senderConsumer.Documents[1].ExpiryDate.ToString("yyyy-MM-dd");                
                }
                
                Console.WriteLine(resultText);
                next_operation_final = "kernel_" + next_operation + "_ok";
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                Console.WriteLine(e.Message);
                //Console.WriteLine(e.Source);
                resultText = e.Message.ToString();
                next_operation_final = "kernel_" + next_operation + "_err";
                result = "1";
            }
            finally
            {
                query = "UPDATE `deltakey`.`exchange` SET `last_operation` = 'check', `next_operation` = '" + next_operation_final + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "', next_try_date = NOW()+INTERVAL 2 SECOND WHERE `exchange`.`transact` = " + transferParams["transact"] + ";";
                ExecQuery(query);
            }
        }

        private static Person CreatePerson(string firstName, string middleName, string lastName, bool isResident, string country)
        {
            WebServiceClient client = new WebServiceClient();
            try
            {
                Person person = new Person();
                person.FirstName = firstName;
                person.MiddleName = middleName;
                person.LastName = lastName;

                Residentship persRes = new Residentship();
                persRes.IsResident = isResident;
                persRes.CountryID = GetCoutryIDByCountryISO(country);
                persRes.ID = person.ID;
                Residentship[] pR = new Residentship[1];
                pR[0] = persRes;
                person.Residentships = pR;

                var request = new CreatePersonRequestMessage()
                {
                    AuthenticationHeader = GetCreds(),
                    Person = person
                };
                var response = client.CreatePerson(request);
                CheckFault(response);
                person = response.Person;
                Console.WriteLine(@" {0} {1} {2} {3} / {4} {5} {6}", person.ID, person.FirstName, person.MiddleName, person.LastName, person.FirstNameLat, person.MiddleNameLat, person.LastNameLat);
                return person;
            }
            finally
            {
                if (client.State == System.ServiceModel.CommunicationState.Opened)
                    client.Close();
            }
        }

        private static Person CreatePersonFull(Dictionary<string, string> transferParams)
        {
            WebServiceClient client = new WebServiceClient();
            try
            {
                Person person = new Person();
                person.FirstName = transferParams["senderFirstName"];
                person.MiddleName = transferParams["senderMiddleName"];
                person.LastName = transferParams["senderLastName"];
                if (transferParams["senderDateBirth"].Length > 0)
                    person.BirthDate = Convert.ToDateTime(transferParams["senderDateBirth"]);
                if (transferParams["senderPlaceBirth"].Length > 0)
                    person.BirthPlace = transferParams["senderPlaceBirth"];

                if (transferParams["phoneNumber"].Length > 0)
                {
                    Phone persPhone = new Phone();
                    persPhone.Type = PhoneType.Mobile;
                    persPhone.CountryID = 18;//Россия
                    persPhone.AreaCode = transferParams["phoneNumber"].Substring(0, 3);
                    persPhone.Number = transferParams["phoneNumber"].Substring(3);
                    Phone[] phoneArr = { persPhone };
                    person.Phones = phoneArr;
                }
                
                if (transferParams["senderRegCountry"].Length > 0)
                {
                    PersonAddress persAddr = new PersonAddress();
                    persAddr.CountryID = GetCoutryIDByCountryISO(transferParams["senderRegCountry"]);
                    persAddr.City = transferParams["senderRegCity"];
                    persAddr.Street = transferParams["senderRegStreet"];
                    persAddr.House = transferParams["senderRegHouse"];
                    persAddr.Building = transferParams["senderRegBuilding"];
                    persAddr.Flat = transferParams["senderRegFlat"];
                    person.Address = persAddr;
                }
                

                if (transferParams["senderDocumentType"].Length > 0)
                {
                    Document persDoc = new Document();
                    persDoc.TypeID = Convert.ToInt32(transferParams["senderDocumentType"]);
                    persDoc.Series = transferParams["senderDocumentSerial"];
                    persDoc.Number = transferParams["senderDocumentNumber"];
                    persDoc.Issuer = transferParams["senderDocumentIssuer"];
                    persDoc.IssueDate = Convert.ToDateTime(transferParams["senderDocumentDateIssue"]);
                    persDoc.IssuerCode = transferParams["senderDocumentDepCode"];

                    if (transferParams["senderNrDocumentType"].Length <= 0)
                    {
                        Document[] pD = new Document[1];
                        pD[0] = persDoc;
                        person.Documents = pD;
                    }
                    else
                    {
                        Document[] pD = new Document[2];
                        pD[0] = persDoc;
                        persDoc = new Document();
                        persDoc.TypeID = Convert.ToInt32(transferParams["senderNrDocumentType"]);
                        persDoc.Series = transferParams["senderNrDocumentSerial"];
                        persDoc.Number = transferParams["senderNrDocumentNumber"];
                        persDoc.Issuer = transferParams["senderNrDocumentIssuer"];                        
                        persDoc.IssuerCode = transferParams["senderNrDocumentDepCode"];
                        persDoc.IssueDate = Convert.ToDateTime(transferParams["senderNrDocumentDateIssue"]);
                        persDoc.ExpiryDate = Convert.ToDateTime(transferParams["senderNrDocumentDateExpire"]);
                        pD[1] = persDoc;
                        person.Documents = pD;
                    }                    
                }


                if (transferParams["senderRegCountry"].Length > 0)
                {
                    Residentship persRes = new Residentship();
                    persRes.IsResident = Convert.ToBoolean(Convert.ToInt32(transferParams["senderIsResident"]));
                    persRes.CountryID = GetCoutryIDByCountryISO(transferParams["senderRegCountry"]);
                    persRes.ID = person.ID;
                    Residentship[] pR = new Residentship[1];
                    pR[0] = persRes;
                    person.Residentships = pR;
                }
                

                var request = new CreatePersonRequestMessage()
                {
                    AuthenticationHeader = GetCreds(),
                    Person = person
                };
                var response = client.CreatePerson(request);
                CheckFault(response);
                person = response.Person;
                Console.WriteLine(@" {0} {1} {2} {3} / {4} {5} {6}", person.ID, person.FirstName, person.MiddleName, person.LastName, person.FirstNameLat, person.MiddleNameLat, person.LastNameLat);
                return person;
            }
            finally
            {
                if (client.State == System.ServiceModel.CommunicationState.Opened)
                    client.Close();
            }
        }

        private static Transfer PrepareTransfer(Person senderConsumer, Person recieverConsumer, double recieverSum, int senderCurr, int recieverCurr, int senderID, int participatorRecieverBank, string controlNumber, out string errorTextPrepareTransfer)
        {
            WebServiceClient unistream = new WebServiceClient();
            
            try
            {

                Transfer transfer = new Transfer();
                transfer.Type = TransferType.Remittance;
                transfer.SentDate = DateTime.Now.Clarify();
                transfer.ControlNumber = controlNumber;
                errorTextPrepareTransfer = "";
                //если валюты совпадают, то перевод в валюте отправителя
                if (recieverCurr == senderCurr)
                {
                    transfer.Amounts = new Amount[]
                                       {
                                           //new Amount() {CurrencyID = senderCurr, Sum = senderSum, Type = AmountType.ActualPaid},
                                           new Amount() {CurrencyID = recieverCurr, Sum = recieverSum, Type = AmountType.Main}
                                       };
                }
                else
                {
                    transfer.Amounts = new Amount[]
                                       {
                                           new Amount() {CurrencyID = senderCurr, Sum = 0, Type = AmountType.ActualPaid},
                                           new Amount() {CurrencyID = recieverCurr, Sum = recieverSum, Type = AmountType.EstimatedPaidout}                                           
                                       };
                }
                
                //transfer.Participators = transfer1.Participators;
                transfer.Participators = new Participator[]
                                              {
                                                  new Participator()
                                                      {
                                                          ID = senderID,
                                                          Role = ParticipatorRole.SenderPOS
                                                      },
                                                  new Participator()
                                                      {
                                                          ID = participatorRecieverBank,
                                                          Role = ParticipatorRole.ExpectedReceiverPOS
                                                      }
                                              };
                
                transfer.Consumers = new Consumer[] { };
                transfer.Consumers = new Consumer[]
                                         {
                                             new Consumer()
                                                 {
                                                     Person = senderConsumer,
                                                     Role = ConsumerRole.Sender
                                                 },
                                             new Consumer()
                                                 {
                                                     Person = recieverConsumer,
                                                     Role = ConsumerRole.ExpectedReceiver
                                                 }
                                         };
                PrepareTransferRequestMessage req = new PrepareTransferRequestMessage()
                {
                    AuthenticationHeader = GetCreds(),
                    Transfer = transfer
                };
                log.WriteCmd("Запрос", "PrepareTransfer");
                log.WriteCmd(Serialize(req), "PrepareTransfer");
                var prepareResult = unistream.PrepareTransfer(req);
                CheckFault(prepareResult);
                CheckTransferRestriction(prepareResult);
                log.WriteCmd("Ответ", "PrepareTransfer");
                log.WriteCmd(Serialize(prepareResult), "PrepareTransfer");
                //Console.WriteLine(Serialize(prepareResult));
                //CheckFault(prepareResult);
                transfer.Amounts = prepareResult.Transfer.Amounts;
                var amounts = transfer.Amounts;
                var services = prepareResult.Transfer.Services.ToList();
                //Console.WriteLine("Transfer.Comment={0}", prepareResult.Transfer.Comment);
                transfer.Comment = prepareResult.Transfer.Comment;
                Console.WriteLine("prepareResult.Transfer.ControlNumber={0}", prepareResult.Transfer.ControlNumber);
                transfer.ControlNumber = prepareResult.Transfer.ControlNumber;
                // выведем комиссии
                services.ForEach(
                    s =>
                    Console.WriteLine(
                        @"s.Fee={0}, s.CurrencyID={1}, s.ParticipatorID={2}, s.Mode={3}, s.ServiceID={4}", s.Fee,
                        s.CurrencyID, s.ParticipatorID, s.Mode, s.ServiceID));
                services.ForEach(
                    s => s.Response = s.Mode == ServiceMode.Required ? Response.Accepted : Response.Rejected);

                double feeSum = 0;
                services.ForEach(
                        s => feeSum += s.Fee
                    );
                Console.WriteLine("Common fee {0}", feeSum);

                transfer.Services = services.ToArray();

                /*transfer.Amounts.ToList().ForEach(
                    s =>
                    Console.WriteLine(
                        @"s.Type={0}, s.CurrencyID={1}, s.Sum={2}", s.Type,
                        s.CurrencyID, s.Sum));
                 * */

                //
                /*if (senderCurr == recieverCurr)
                {
                    transfer.Amounts = new Amount[]
                                       {   transfer.Amounts[0],
                                           //transfer.Amounts[1],
                                           new Amount()
                                               {
                                                   CurrencyID = senderCurr,
                                                   Sum = feeSum,
                                                   Type = AmountType.PrimaryPaidComission
                                               }
                                       };
                }*/
                /*  else
                  {
                      Array.Resize(ref amounts, transfer.Amounts.Length + 1);
                      amounts[amounts.Length - 1] = 
                                             new Amount()
                                                 {
                                                     CurrencyID = senderCurr,
                                                     Sum = feeSum,
                                                     Type = AmountType.PrimaryPaidComission
                                                 };

                      Amount am = new Amount()
                                      {
                                          CurrencyID = senderCurr,
                                          Sum = feeSum,
                                          Type = AmountType.PrimaryPaidComission
                                      };
                      amounts[3] = am;
                      transfer.Amounts = amounts;
                  }*/
                /*
                transfer.CashierUserAction = new UserActionInfo()
                {
                    ActionLocalDateTime = DateTime.Now.Clarify2(),
                    UserID = 1,
                    UserUnistreamCard = "hello1"
                };

                transfer.TellerUserAction = new UserActionInfo()
                {
                    ActionLocalDateTime = DateTime.Now.Clarify2(),
                    UserID = 2,
                    UserUnistreamCard = "hello2"
                };
                 * */
                return transfer;
            }
            catch (System.Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                Console.WriteLine(e.Message);
                Console.WriteLine("PrepareTransfer Exception");
                errorTextPrepareTransfer = e.Message;
                return null;
            }
            finally
            {
                if (unistream.State == System.ServiceModel.CommunicationState.Opened)
                    unistream.Close();
            }
        }


        private static Transfer PrepareTransferLite(double recieverSum, int senderCurr, int recieverCurr, int senderID, int participatorRecieverBank)
        {
            WebServiceClient unistream = new WebServiceClient();
            
            try
            {
                Transfer transfer = new Transfer();
                transfer.Type = TransferType.Remittance;
                transfer.SentDate = DateTime.Now.Clarify();
                transfer.ControlNumber = "";
                if (recieverCurr == senderCurr)
                {
                    transfer.Amounts = new Amount[]
                                       {
                                           new Amount() {CurrencyID = senderCurr, Sum = 0, Type = AmountType.ActualPaid},
                                           new Amount() {CurrencyID = recieverCurr, Sum = recieverSum, Type = AmountType.Main}
                                       };
                }
                else
                {
                    transfer.Amounts = new Amount[]
                                       {
                                           new Amount() {CurrencyID = senderCurr, Sum = 0, Type = AmountType.ActualPaid},
                                           new Amount() {CurrencyID = recieverCurr, Sum = recieverSum, Type = AmountType.EstimatedPaidout}                                           
                                       };
                }
                transfer.Participators = new Participator[]
                                              {
                                                  new Participator()
                                                      {
                                                          ID = senderID,
                                                          Role = ParticipatorRole.SenderPOS
                                                      },
                                                  new Participator()
                                                      {
                                                          ID = participatorRecieverBank,
                                                          Role = ParticipatorRole.ExpectedReceiverPOS
                                                      }
                                              };

                transfer.Consumers = null;
                PrepareTransferRequestMessage req = new PrepareTransferRequestMessage()
                {
                    AuthenticationHeader = GetCreds(),
                    Transfer = transfer
                };
                log.WriteCmd("Запрос", "PrepareTransfer");
                log.WriteCmd(Serialize(req), "PrepareTransfer");
                var prepareResult = unistream.PrepareTransfer(req);
                CheckFault(prepareResult);
                CheckTransferRestriction(prepareResult);
                log.WriteCmd("Ответ", "PrepareTransfer");
                log.WriteCmd(Serialize(prepareResult), "PrepareTransfer");
                transfer.Amounts = prepareResult.Transfer.Amounts;
                var amounts = prepareResult.Transfer.Amounts;
                var services = prepareResult.Transfer.Services.ToList();
                double feeSum = 0;
                services.ForEach(
                        s => feeSum += s.Fee
                    );                
                transfer.Amounts = new Amount[]
                                   {   transfer.Amounts[0],
                                       transfer.Amounts[1],
                                       new Amount()
                                           {
                                               CurrencyID = senderCurr,
                                               Sum = feeSum,
                                               Type = AmountType.PrimaryPaidComission
                                           }
                                   };

                return prepareResult.Transfer;
            }
            catch (System.Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                Console.WriteLine(e.Message);
                Console.WriteLine("PrepareTransferLite Exception");
                return null;
            }
            finally
            {
                if (unistream.State == System.ServiceModel.CommunicationState.Opened)
                    unistream.Close();
            }
        }

        private static void FindPerson(Dictionary<string, string> transferParams)
        {
            string nextOperation = "kernel_" + transferParams["nextOperation"] + "_ok";
            string resultText = "";
            string query = "";
            string result = "0";
            string status = "2";
            try
            {
                WebServiceClient client = new WebServiceClient();
                Transfer transfer = new Transfer();
                
                var senderConsumer = new Person();
                if (FindPerson(transferParams["senderFirstName"], transferParams["senderMiddleName"], transferParams["senderLastName"], transferParams["phoneNumber"], out senderConsumer))
                {
                    resultText = senderConsumer.LastName;
                    resultText += '&' + senderConsumer.FirstName;
                    resultText += '&' + senderConsumer.MiddleName;

                    //var tm = Convert.ToInt16(senderConsumer.Residentships[0].IsResident);
                    resultText += '&' + (Convert.ToInt16(senderConsumer.Residentships[0].IsResident)).ToString();
                    resultText += '&' + (senderConsumer.Documents[0].TypeID).ToString();
                    resultText += '&' + senderConsumer.Documents[0].Series;
                    resultText += '&' + senderConsumer.Documents[0].Number;
                    resultText += '&' + senderConsumer.Documents[0].IssueDate.ToString("yyyy-MM-dd");
                    resultText += '&' + senderConsumer.Documents[0].Issuer;
                    resultText += '&' + senderConsumer.Documents[0].IssuerCode;

                    resultText += '&' + senderConsumer.Phones[0].AreaCode + senderConsumer.Phones[0].Number;

                    resultText += '&' + senderConsumer.BirthDate.ToString("yyyy-MM-dd");
                    resultText += '&' + senderConsumer.BirthPlace;

                    resultText += '&' + GetCoutryISOByCountryID(senderConsumer.Address.CountryID);
                    resultText += '&';//region
                    resultText += '&' + senderConsumer.Address.City;
                    resultText += '&' + senderConsumer.Address.Street;
                    resultText += '&' + senderConsumer.Address.House;
                    resultText += '&' + senderConsumer.Address.Building;
                    resultText += '&' + senderConsumer.Address.Flat;
                    resultText += '&' + senderConsumer.Address.PostalCode;
                }
                else
                {
                    resultText = "&&&&&&&&&&&&&&&&&&&&";
                }
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                Console.WriteLine("FindPerson Exception");
                nextOperation = "kernel_" + transferParams["nextOperation"] + "_err";
                resultText = e.Message;
                result = "1";
                Console.WriteLine(e.Message);
            }
            finally
            {
                query = "UPDATE `deltakey`.`exchange` SET `last_operation` = '" + transferParams["nextOperation"] + "', `next_operation` = '" + nextOperation + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "' WHERE `exchange`.`transact` = '" + transferParams["transact"] + "';";
                ExecQuery(query);
            }
        }

        private static void EstimateMainAmount(Dictionary<string, string> transferParams)
        {            
            //int senderID = 294278;
            var participatorRecieverBank = GetVirtBankByCountry(Convert.ToInt32(transferParams["countryPayout"]));
            string nextOperation = "kernel_" + transferParams["nextOperation"] + "_ok";
            string resultText = "";
            string query = "";
            string result = "0";
            string status = "2";
            try
            {
                WebServiceClient client = new WebServiceClient();
                Transfer transfer = new Transfer();
                
                transfer.Type = TransferType.Remittance;
                transfer.SentDate = DateTime.SpecifyKind(DateTime.Now.Date, DateTimeKind.Unspecified);
                var recieverCurr = GetUnistreamCurr(Convert.ToInt32(transferParams["sumOutCurr"]));
                var senderCurr = GetUnistreamCurr(Convert.ToInt32(transferParams["sumInCurr"]));
                var totalAmount = Convert.ToDouble(transferParams["sumIn"]);
                //если валюты совпадают, то перевод в валюте отправителя
                if (recieverCurr == senderCurr)
                {
                    transfer.Amounts = new Amount[]
                                       {
                                           new Amount() { CurrencyID = senderCurr, Sum = 0, Type = AmountType.Main },
                                       };
                }
                else
                {
                    transfer.Amounts = new Amount[]
                                       {
                                           new Amount() { CurrencyID = senderCurr, Sum = 0, Type = AmountType.ActualPaid },
                                           new Amount() { CurrencyID = recieverCurr, Sum = 0, Type = AmountType.EstimatedPaidout },
                                       };
                }

                transfer.Participators = new Participator[]
                                             {
                                                 new Participator()
                                                     {
                                                         ID = senderID,
                                                         Role = ParticipatorRole.SenderPOS
                                                     },
                                                 new Participator()
                                                     {
                                                         ID = participatorRecieverBank,
                                                         Role = ParticipatorRole.ExpectedReceiverPOS
                                                     }
                                             };
                var req = new EstimateMainAmountRequestMessage()
                {
                    AuthenticationHeader = GetCreds(),
                    Transfer = transfer,
                    TotalAmount = totalAmount
                };
                log.WriteCmd("Запрос", "EstimateMainAmount");
                log.WriteCmd(Serialize(req), "EstimateMainAmount");
                var estimateResult = client.EstimateMainAmount(req);
                log.WriteCmd("Ответ", "EstimateMainAmount");
                log.WriteCmd(Serialize(estimateResult), "EstimateMainAmount");
                CheckFault(estimateResult);
                estimateResult.Transfer.Amounts.ToList().ForEach(
                    a =>
                    Console.WriteLine(
                        @"a.CurrencyID={0}, a.Type={1}, a.Sum={2}",
                        a.CurrencyID, a.Type, a.Sum));                
                Console.WriteLine("Комиссии:");
                // выведем комиссии
                estimateResult.Transfer.Services.ToList().ForEach(
                    s =>
                    Console.WriteLine(
                        @"s.Fee={0}, s.CurrencyID={1}, s.ParticipatorID={2}, s.Mode={3}, s.ServiceID={4}", s.Fee,
                        s.CurrencyID, s.ParticipatorID, s.Mode, s.ServiceID));

                /*
                answer_params['fee'] = '60.00'
                answer_params['fee_curr'] = '810'
                answer_params['sum_in'] = '3000.00'
                answer_params['sum_in_curr'] = '810'			
                answer_params['sum_out'] = '100'
                answer_params['sum_out_curr'] = '840'                 
                 */
                double feeSum = 0;
                estimateResult.Transfer.Services.ToList().ForEach(
                        s => feeSum += s.Fee
                    );
                Console.WriteLine("Common fee {0}", feeSum);
                //return estimateResult.Transfer;
                resultText = feeSum.ToString() +"&"+transferParams["sumInCurr"]+"&"+transferParams["sumIn"]+"&"+transferParams["sumInCurr"]+"&";
                var rate = "";
                if (recieverCurr != senderCurr)
                {
                    estimateResult.Transfer.Amounts.ToList().ForEach(
                        c => resultText += GetEstimatedPayoutSum(c)
                        );
                    estimateResult.Transfer.Amounts.ToList().ForEach(
                        c => rate += GetRate(c)
                        );
                }
                else
                {
                    estimateResult.Transfer.Amounts.ToList().ForEach(
                        c => resultText += GetMainSum(c)
                        );
                }                
                resultText += "&" + transferParams["sumOutCurr"];
                resultText += "&" + rate;
                Console.WriteLine(resultText);
                resultText = resultText.Replace(',', '.');
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                Console.WriteLine("EstimateMainAmount Exception");
                nextOperation = "kernel_" + transferParams["nextOperation"] + "_err";
                resultText = e.Message;
                result = "1";
                Console.WriteLine(e.Message);
                //return null;
            }
            finally
            {
                query = "UPDATE `deltakey`.`exchange` SET `last_operation` = '" + transferParams["nextOperation"] + "', `next_operation` = '" + nextOperation + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "' WHERE `exchange`.`transact` = '" + transferParams["transact"] + "';";
                //Console.WriteLine(query);
                ExecQuery(query);
            }
        }

        private static string GetRate(Amount c)
        {
            //if (String.Compare(c.Type.ToString(), AmountType.EstimatedPaidout.ToString()) == 0)
            if (c.Rate > 0)
                return c.Rate.ToString();
            return null;
        }

        private static void CheckPayoutPosibility(Dictionary<string, string> transferParams)
        {
            //throw new NotImplementedException();
            WebServiceClient unistream = new WebServiceClient();
            string nextOperation = "kernel_" + transferParams["nextOperation"] + "_ok";
            string resultText = "";
            string query = "";
            string result = "0";
            string status = "2";
            //Transfer transfer;
            
            //int senderID = 294278;
            var controlNumber = transferParams["controlNumber"];
             
            try
            {
                //FindTransfer
                var findResponse = unistream.FindTransfer(new FindTransferRequestMessage
                {
                    AuthenticationHeader = GetCreds(),
                    ControlNumber = controlNumber,
                    CurrencyID = GetUnistreamCurr(Convert.ToInt32(transferParams["recieverCurr"])),
                    Sum = Convert.ToDouble(transferParams["amount"]),
                    BankID = senderID
                });
                CheckFault(findResponse);
                if (findResponse.Transfer == null)
                    throw new ApplicationException("transfer not found"); //throw new ApplicationException("По указаннным критериям перевод доступный для выдачи из текущего пункта не найден");
                Console.WriteLine("Комиссии найденного:");
                // выведем комиссии
                findResponse.Transfer.Services.ToList().ForEach(
                    s =>
                    Console.WriteLine(
                        @"s.Fee={0}, s.CurrencyID={1}, s.ParticipatorID={2}, s.Mode={3}, s.ServiceID={4}, Response={5}", s.Fee,
                        s.CurrencyID, s.ParticipatorID, s.Mode, s.ServiceID, s.Response));

                Console.WriteLine(
                    @"findResponse.Transfer.ID={0}, findResponse.Transfer.SentDate={1}, findResponse.Transfer.Status={2}",
                    findResponse.Transfer.ID, findResponse.Transfer.SentDate, findResponse.Transfer.Status);


                Console.WriteLine("Клиенты найденного:");
                findResponse.Transfer.Consumers.ToList().ForEach(
                    c =>
                    Console.WriteLine(
                        @"c.Role={0}, c.Person.FirstName={1}, c.Person.MiddleName={2}, c.Person.LastName={3}, c.Person.UnistreamCardNumber={4}",
                        c.Role, c.Person.FirstName, c.Person.MiddleName, c.Person.LastName, c.Person.UnistreamCardNumber));

                //возвратить сумму и имена

                if (String.Compare(findResponse.Transfer.Consumers[1].Role.ToString(), "ExpectedReceiver") == 0)
                {
                    resultText = findResponse.Transfer.Consumers[1].Person.LastName + '&' + findResponse.Transfer.Consumers[1].Person.FirstName + '&' + findResponse.Transfer.Consumers[1].Person.MiddleName;
                    Console.WriteLine(resultText);
                }
               
                findResponse.Transfer.Amounts.ToList().ForEach(
                    c => resultText += "&"+GetEstimatedPayoutSum(c)                       
                    );
                resultText = Convert.ToBase64String(GetBytes(resultText));
                /*
                //FindPerson - рано ещё использовать
                //CreatePerson
                throw new Exception("too early");
                transfer = findResponse.Transfer;
                var consumers = transfer.Consumers.ToList();
                consumers.Add(
                            new Consumer()
                            {
                                Person = CreatePerson(transferParams["senderFirstName"], transferParams["senderMiddleName"], transferParams["senderLastName"]),
                                Role = ConsumerRole.ActualReceiver
                            }
                    );
                transfer.Consumers = consumers.ToArray();

                // добавим фактический пункт выплаты
                var participators = transfer.Participators.ToList();
                participators.Add(
                    new Participator
                    {
                        ID = senderID,
                        Role = ParticipatorRole.ActualReceiverPOS
                    }
                    );
                transfer.Participators = participators.ToArray();


                log.WriteCmd("Запрос", "PayoutTransfer");
                log.WriteCmd(Serialize(transfer), "PayoutTransfer");
                //PayoutTransfer
                var getResponse = unistream.PayoutTransfer(new PayoutTransferRequestMessage() { AuthenticationHeader = GetCreds(), Transfer = transfer });
                log.WriteCmd("Ответ", "PayoutTransfer");
                log.WriteCmd(Serialize(getResponse), "PayoutTransfer");
                CheckFault(getResponse);
                */
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                nextOperation = "kernel_" + transferParams["nextOperation"] + "_err";
                resultText = e.Message;
                result = "1";
                Console.WriteLine(e.Message);
                //Console.WriteLine(e.Source);
            }
            finally
            {
                query = "UPDATE `deltakey`.`exchange` SET `last_operation` = '" + transferParams["nextOperation"] + "', `next_operation` = '" + nextOperation + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "' WHERE `exchange`.`transact` = '" + transferParams["transact"] + "';";
                //Console.WriteLine(query);
                ExecQuery(query);
            }
        }

        private static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static string ConvertStringToHex(string asciiString)
        {
            string hex = "";
            foreach (char c in asciiString)
            {
                int tmp = c;
                hex += String.Format("{0:x2}", (uint)System.Convert.ToUInt32(tmp.ToString()));
            }
            return hex;
        }

        private static string GetEstimatedPayoutSum(Amount amount)
        {
            if (String.Compare(amount.Type.ToString(), AmountType.EstimatedPaidout.ToString()) == 0 || String.Compare(amount.Type.ToString(), AmountType.ActualPaidout.ToString()) == 0)
                return amount.Sum.ToString();
            return null;
        }
        
        private static string GetMainSum(Amount amount)
        {
            if (String.Compare(amount.Type.ToString(), AmountType.Main.ToString()) == 0)
                return amount.Sum.ToString();
            return null;
        }
        
        private static Dictionary<string, string> GetCheckParams()
        {
            throw new NotImplementedException();
        }

        /*
         * из таблицы exchange получает одну запись
         * возвращает string содержимое поля next_operation
         * ещё нужен номер транзакции, чтобы след. шагом запросить пар-ры... 
         * или нет, оставить текущую схему, но парсить параметры в завис-ти от nextOperation
         */
        private static string GetNextOperation()
        {
            string nextOperation = "null";
            string Connect = "Database=deltakey;Data Source=deltakey.net;User Id=exchange;Password=yj7h4MyR2YUh933x";
            MySqlConnection myConnection = new MySqlConnection(Connect);
            string CommandText = "SELECT transact, next_operation FROM  `exchange` WHERE  `how` =102 AND `next_operation` IN ('check','check_and_die','pay','status','cancel','cancel_wait','return','payout') LIMIT 1";
            myConnection.Open();
            MySqlCommand myCommand = new MySqlCommand(CommandText, myConnection);
            MySqlDataReader MyDataReader = myCommand.ExecuteReader();
            try
            {
                if (MyDataReader.Read())
                    nextOperation = MyDataReader.GetValue(0).ToString();
            }
            finally
            {
                MyDataReader.Close();
                myConnection.Close();
            }
            //Console.WriteLine(nextOperation);
            return nextOperation;
        }

        private static void PayoutTransfer(Dictionary<string, string> transferParams)
        {
            /* WebServiceClient unistream = new WebServiceClient();
             string nextOperation = "kernel_payout_ok";
             string resultText = "";
             string query = "";
             string result = "0";
             string status = "2";
             Transfer transfer;
             
             //int senderID = 294278;
             //откуда взять контрольный код? 
             //его введет оператор
             //значит из параметров
             //значит для каждой операции нужен свой набор параметров
             var controlNumber = "212708852406";
             * */
            throw new NotImplementedException();

            /*
            try
            {
                //FindTransfer
                var findResponse = unistream.FindTransfer(new FindTransferRequestMessage
                {
                    AuthenticationHeader = GetCreds(),
                    ControlNumber = controlNumber,
                    CurrencyID = 1,
                    Sum = 3000,
                    BankID = senderID
                });
                CheckFault(findResponse);
                if (findResponse.Transfer == null)
                    throw new ApplicationException("По указаннным критериям перевод доступный для выдачи из текущего пункта не найден");
                Console.WriteLine("Комиссии найденного:");
                // выведем комиссии
                findResponse.Transfer.Services.ToList().ForEach(
                    s =>
                    Console.WriteLine(
                        @"s.Fee={0}, s.CurrencyID={1}, s.ParticipatorID={2}, s.Mode={3}, s.ServiceID={4}, Response={5}", s.Fee,
                        s.CurrencyID, s.ParticipatorID, s.Mode, s.ServiceID, s.Response));

                Console.WriteLine(
                    @"findResponse.Transfer.ID={0}, findResponse.Transfer.SentDate={1}, findResponse.Transfer.Status={2}",
                    findResponse.Transfer.ID, findResponse.Transfer.SentDate, findResponse.Transfer.Status);


                Console.WriteLine("Клиенты найденного:");
                findResponse.Transfer.Consumers.ToList().ForEach(
                    c =>
                    Console.WriteLine(
                        @"c.Role={0}, c.Person.FirstName={1}, c.Person.MiddleName={2}, c.Person.LastName={3}, c.Person.UnistreamCardNumber={4}",
                        c.Role, c.Person.FirstName, c.Person.MiddleName, c.Person.LastName, c.Person.UnistreamCardNumber));


                //FindPerson - рано ещё использовать
                //CreatePerson
                transfer = findResponse.Transfer;
                var consumers = transfer.Consumers.ToList();
                consumers.Add(
                            new Consumer()
                            {
                                Person = CreatePerson(transferParams["senderFirstName"], transferParams["senderMiddleName"], transferParams["senderLastName"]),
                                Role = ConsumerRole.ActualReceiver
                            }
                    );
                transfer.Consumers = consumers.ToArray();

                // добавим фактический пункт выплаты
                var participators = transfer.Participators.ToList();
                participators.Add(
                    new Participator
                    {
                        ID = senderID,
                        Role = ParticipatorRole.ActualReceiverPOS
                    }
                    );
                transfer.Participators = participators.ToArray();


                log.WriteCmd("Запрос", "PayoutTransfer");
                log.WriteCmd(Serialize(transfer), "PayoutTransfer");
                //PayoutTransfer
                var getResponse = unistream.PayoutTransfer(new PayoutTransferRequestMessage() { AuthenticationHeader = GetCreds(), Transfer = transfer });
                log.WriteCmd("Ответ", "PayoutTransfer");
                log.WriteCmd(Serialize(getResponse), "PayoutTransfer");
                CheckFault(getResponse);
            }
            catch (Exception e)
            {
                nextOperation = "kernel_payout_err";
                resultText = e.Message;
                result = "1";
                Console.WriteLine(e.Message);
                //Console.WriteLine(e.Source);
            }
            finally
            {
                query = "UPDATE `deltakey`.`exchange` SET `last_operation` = 'payout', `next_operation` = '" + nextOperation + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "' WHERE `next_operation` = 'payout' AND `exchange`.`transact` = " + transferParams["transact"] + ";";
                ExecQuery(query);
            }
             * */
        }

        private static void ReturnTransfer(Dictionary<string, string> transferParams)
        {
            WebServiceClient unistream = new WebServiceClient();
            string nextOperation = "kernel_return_ok";
            string resultText = "";
            string query = "";
            string result = "0";
            string status = "2";
            Transfer transfer;
            
            //сменился 020312
            //int senderID = 294278;
            try
            {
                transfer = GetTransferBySourceID(transferParams["transact"]);
                // добавим фактического получателя
                var consumers = transfer.Consumers.ToList();
                consumers.Add(
                            new Consumer()
                            {
                                Person = CreatePerson(transferParams["senderFirstName"], transferParams["senderMiddleName"], transferParams["senderLastName"]),
                                Role = ConsumerRole.ActualReceiver
                            }
                    );
                transfer.Consumers = consumers.ToArray();
                // добавим фактический пункт выплаты
                var participators = transfer.Participators.ToList();
                participators.Add(
                    new Participator
                    {
                        ID = senderID,
                        Role = ParticipatorRole.ActualReceiverPOS
                    }
                    );
                transfer.Participators = participators.ToArray();


                log.WriteCmd("Запрос", "ReturnTransfer");
                log.WriteCmd(Serialize(transfer), "ReturnTransfer");
                var getResponse = unistream.ReturnTransfer(new ReturnTransferRequestMessage() { AuthenticationHeader = GetCreds(), Transfer = transfer });
                log.WriteCmd("Ответ", "ReturnTransfer");
                log.WriteCmd(Serialize(getResponse), "ReturnTransfer");
                CheckFault(getResponse);
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                nextOperation = "kernel_return_err";
                resultText = e.Message;
                result = "1";
                Console.WriteLine(e.Message);
                //Console.WriteLine(e.Source);
            }
            finally
            {
                query = "UPDATE `deltakey`.`exchange` SET `last_operation` = 'return', `next_operation` = '" + nextOperation + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "' WHERE `exchange`.`transact` = " + transferParams["transact"] + ";";
                ExecQuery(query);
            }
        }

        private static Transfer GetTransferBySourceID(string sourceID)
        {
            WebServiceClient unistream = new WebServiceClient();
            // получим перевод по sourceID
            var getResponse =
                unistream.GetTransferBySourceID(new GetTransferBySourceIDRequestMessage() { AuthenticationHeader = GetCreds(), SourceID = sourceID });
            CheckFault(getResponse);
            return getResponse.Transfer;
        }

        private static void GetCancelTransferAnswer(Dictionary<string, string> transferParams)
        {
            WebServiceClient unistream = new WebServiceClient();
            string nextOperation = "kernel_cancel_ok";
            string resultText = "";
            string result = "0";
            string status = "2";
            string query = "";
            try
            {
                string sourceID = transferParams["transact"];
                // получим перевод по sourceID
                var getResponse =
                    unistream.GetTransferBySourceID(new GetTransferBySourceIDRequestMessage() { AuthenticationHeader = GetCreds(), SourceID = sourceID });
                CheckFault(getResponse);
                //if (getResponse.Transfer.NoticeList.Length > 0)
                //{
                //Console.WriteLine("findResponse.Transfer.NoticeList={0}", Serialize(getResponse.Transfer.NoticeList));
                //    resultText = "cancel impossible";
                //}

                throw new NotImplementedException();
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                nextOperation = "kernel_cancel_err";
                resultText = "error";
                result = "1";
                Console.WriteLine(e.Message);
                //Console.WriteLine(e.Source);
            }
            finally
            {
                query = "UPDATE `deltakey`.`exchange` SET `last_operation` = 'cancel_wait', `next_operation` = '" + nextOperation + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "' WHERE `next_operation` = 'cancel_wait' AND `exchange`.`transact` = " + transferParams["transact"] + ";";
                ExecQuery(query);
            }
        }

        private static void CancelTransfer(Dictionary<string, string> transferParams)
        {
            WebServiceClient unistream = new WebServiceClient();
            string nextOperation = "kernel_cancel_ok";
            string resultText = "";
            string result = "0";
            string status = "2";
            string query = "";
            try
            {
                string sourceID = transferParams["transact"];
                // получим перевод по sourceID
                var getResponse =
                    unistream.GetTransferBySourceID(new GetTransferBySourceIDRequestMessage() { AuthenticationHeader = GetCreds(), SourceID = sourceID });
                CheckFault(getResponse);
                switch (getResponse.Transfer.Status)
                {
                    case TransferStatus.Paid:
                    case TransferStatus.None:
                    case TransferStatus.Rejected:
                        resultText = "cancel impossible";
                        break;
                    case TransferStatus.Cancelled:
                        resultText = "transfer already cancelled";
                        break;
                    case TransferStatus.Accepted:
                        if (getResponse.Transfer.NoticeList.Length > 0)
                        {
                            Console.WriteLine("findResponse.Transfer.NoticeList={0}", Serialize(getResponse.Transfer.NoticeList));
                            resultText = "cancel impossible";
                        }
                        else
                        {
                            List<string> answer = new List<string>();
                            answer = TryCancelTransfer(getResponse.Transfer);
                            nextOperation = answer[1];
                            resultText = answer[0];
                        }
                        break;
                    default:
                        resultText = "cancel impossible";
                        break;
                }
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                nextOperation = "kernel_cancel_err";
                resultText = "error";
                result = "1";
                Console.WriteLine(e.Message);
                //Console.WriteLine(e.Source);
            }
            finally
            {
                query = "UPDATE `deltakey`.`exchange` SET `last_operation` = 'cancel', `next_operation` = '" + nextOperation + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "' WHERE `next_operation` = 'cancel' AND `exchange`.`transact` = '" + transferParams["transact"] + "';";
                ExecQuery(query);
            }
        }

        private static List<string> TryCancelTransfer(Transfer transfer)
        {
            WebServiceClient unistream = new WebServiceClient();
            List<string> answer = new List<string>();
            try
            {
                //prepare notice
                //insert notice
                // вставим уведомление 
                var notice = new Notice();
                notice.Type = NoticeType.CancelTransfer;
                notice.Transfer = transfer;

                var prepNoticeRequest = new PrepareNoticeRequestMessage();
                prepNoticeRequest.AuthenticationHeader = GetCreds();
                prepNoticeRequest.Notice = notice;
                var prepNoticeResponse = unistream.PrepareNotice(prepNoticeRequest);
                CheckFault(prepNoticeResponse);

                var insertNoticeRequest = new InsertNoticeRequestMessage();
                insertNoticeRequest.AuthenticationHeader = GetCreds();
                insertNoticeRequest.Notice = notice;

                var insertNoticeResponse = unistream.InsertNotice(insertNoticeRequest);
                CheckFault(insertNoticeResponse);

                Console.WriteLine("insertNoticeResponse.Notice.ID={0}, insertNoticeResponse.Notice.Status={1}",
                    insertNoticeResponse.Notice.ID, insertNoticeResponse.Notice.Status);
                //проверить статус нотиса
                //если approved, проверить статус перевода, если cancelled - OK                    

                switch (insertNoticeResponse.Notice.Status)
                {
                    case NoticeStatus.Approved:
                    case NoticeStatus.None:
                        answer.Add("wait");
                        answer.Add("cancel");
                        return answer;
                    case NoticeStatus.Accepted:
                        answer.Add("wait");
                        answer.Add("cancel_wait");
                        return answer;
                    default:
                        answer.Add("cancel impossible");
                        answer.Add("kernel_cancel_ok");
                        return answer;
                }
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                answer.Add(e.ToString());
                answer.Add("kernel_cancel_err");
                return answer;
            }
        }

        private static void GetTransferBySourceID(Dictionary<string, string> transferParams)
        {
            WebServiceClient unistream = new WebServiceClient();
            string nextOperation = "kernel_status_ok";
            string resultText = "";
            string result = "0";
            string status = "2";
            string query = "";
            
            try
            {
                string sourceID = transferParams["transact"];
                // получим перевод по sourceID
                var getResponse =
                    unistream.GetTransferBySourceID(new GetTransferBySourceIDRequestMessage() { AuthenticationHeader = GetCreds(), SourceID = sourceID });
                CheckFault(getResponse);
                log.WriteCmd("Ответ", "GetTransferBySourceID");
                log.WriteCmd(Serialize(getResponse), "GetTransferBySourceID");

                Console.WriteLine(
                    @"getResponse.Transfer.ID={0}, getResponse.Transfer.SourceID={1}, getResponse.Transfer.ControlNumber={2}",
                    getResponse.Transfer.ID, getResponse.Transfer.SourceID, getResponse.Transfer.ControlNumber);

                //Debug.Assert(insertTransferResponse.Transfer.ID, getResponse.Transfer.ID);

                //Console.WriteLine("Комиссии :");
                // выведем комиссии
                /*getResponse.Transfer.Services.ToList().ForEach(
                    s =>
                    Console.WriteLine(
                        @"s.Fee={0}, s.CurrencyID={1}, s.ParticipatorID={2}, s.Mode={3}, s.ServiceID={4}, Response={5}",
                        s.Fee,
                        s.CurrencyID, s.ParticipatorID, s.Mode, s.ServiceID, s.Response));*/
                resultText = getResponse.Transfer.Status.ToString();
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                Console.WriteLine(e.Message);
                nextOperation = "kernel_status_err";
                result = "1";
                //Console.WriteLine(e.Source);
            }
            finally
            {
                query = "UPDATE `deltakey`.`exchange` SET `last_operation` = 'status', `next_operation` = '" + nextOperation + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "' WHERE `next_operation` = 'status' AND `exchange`.`transact` = " + transferParams["transact"] + ";";
                ExecQuery(query);
            }
        }

        private static string _GetTransferStatusBySourceID(string _sourceID)
        {
            WebServiceClient unistream = new WebServiceClient();
            try
            {
                // получим перевод по sourceID
                var getResponse =
                    unistream.GetTransferBySourceID(new GetTransferBySourceIDRequestMessage() { AuthenticationHeader = GetCreds(), SourceID = _sourceID });
                string res = RetCheckFault(getResponse);
                if (res == "OK")
                    return getResponse.Transfer.Status.ToString();
                else
                    return res;
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                //Console.WriteLine(e.Source);
                throw new Exception(e.Message);

            }
        }

        private static void PayTransfer(Dictionary<string, string> transferParams)
        {
            string resultText = "";
            string nextOperation = "kernel_ok";
            string query = "";
            string result = "0";
            string status = "2";
            try
            {
                string senderFirstName = transferParams["senderFirstName"];//transferParams[senderFirstName]
                string senderMiddleName = transferParams["senderMiddleName"];
                string senderLastName = transferParams["senderLastName"];
                string recieverFirstName = transferParams["recieverFirstName"];
                string recieverMiddleName = transferParams["recieverMiddleName"];
                string recieverLastName = transferParams["recieverLastName"];
                double senderSum = Convert.ToDouble(transferParams["amount"]);// 100;//RUB
                int country = Convert.ToInt32(transferParams["country"]);//643;
                var sourceID = Convert.ToInt64(transferParams["transact"]);
                string next_operation = transferParams["nextOperation"];
                /*
                 *Настройки:
                 * ИД банка отправителя(Дельтакей)
                 * Валюта
                 * Ключ приложения
                 * Имя пользователя
                 * Пароль
                 * База данных
                 * Хост
                 * Пользователь БД
                 * Пароль БД
                 * 
                 */
                //int senderID = 294278;
                string controlNumber = transferParams["controlNumber"];
                int senderCurr = GetUnistreamCurr(Convert.ToInt32(transferParams["senderCurr"]));
                int recieverCurr = GetUnistreamCurr(Convert.ToInt32(transferParams["recieverCurr"]));

                //создаем клиентов перевода
                //var senderConsumer = CreatePerson(senderFirstName, senderMiddleName, senderLastName, transferParams["phoneNumber"]);
                var senderConsumer = new Person();
                Console.WriteLine("Номер телефона отправителя = {0}", transferParams["phoneNumber"]);
                if (!FindPerson(senderFirstName, senderMiddleName, senderLastName, transferParams["phoneNumber"], out senderConsumer))
                    senderConsumer = CreatePerson(senderFirstName, senderMiddleName, senderLastName, transferParams["phoneNumber"]);

                var recieverConsumer = CreatePerson(recieverFirstName, recieverMiddleName, recieverLastName);

                //по коду страны получаем участника перевода - банк получатель
                var participatorRecieverBank = GetVirtBankByCountry(country);

                //подготавливаем перевод
                var errorTextPrepareTransfer = "";
                Transfer transfer = EstimateMainAmount(senderSum, senderCurr, recieverCurr, senderID, participatorRecieverBank);
                transfer = PrepareTransfer(senderConsumer, recieverConsumer, senderSum, senderCurr, recieverCurr, senderID, participatorRecieverBank, controlNumber, transfer, out errorTextPrepareTransfer);
                if (transfer == null)
                {
                    throw new Exception(errorTextPrepareTransfer);
                }
                ArrayList iTR = InsertTransfer(transfer, sourceID);
                Console.WriteLine("Номер перевода - {0}, контрольный номер - {1}, статус - {2}, номер в системе отправителя - {3}", iTR[0].ToString(), iTR[1].ToString(), iTR[2].ToString(), iTR[3].ToString());
                resultText = iTR[0].ToString() + "&" + iTR[1].ToString() + "&" + iTR[2].ToString() + "&" + iTR[3].ToString();
                //resultText = String.Format("Номер перевода - {0}, контрольный номер - {1}, номер в системе отправителя - {2}", iTR[0].ToString(), iTR[1].ToString(), iTR[3].ToString());

            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                Console.WriteLine(e.Message);
                //Console.WriteLine(e.Source);
                resultText = e.Message;
                nextOperation = "kernel_err";
                result = "1";
            }
            finally
            {
                query = "UPDATE `deltakey`.`exchange` SET `last_operation` = 'pay', `next_operation` = '" + nextOperation + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "' WHERE `next_operation` = 'pay' AND `exchange`.`transact` = " + transferParams["transact"] + ";";
                ExecQuery(query);
            }
        }

        private static int GetUnistreamCurr(int curr)
        {
            switch (curr)
            {
                case 840:
                    return 2;
                case 978:
                    return 3;
                case 810:
                    return 1;
                default:
                    throw new Exception("no such curr");
            }
        }

        /*
         * метод ничего не проверяет, только изменят запись в базе на "проверено, готово к оплате"
         * 
         * 
         */
        private static void CheckTransferPosibility(Dictionary<string, string> transferParams)
        {
            WebServiceClient unistream = new WebServiceClient();
            string next_operation = "";
            string next_operation_final = "";
            string resultText = "";
            string status = "2";
            string result = "0";
            string query = "";
            try
            {
                //проверить существует перевод с таким source id ?       
                resultText = _GetTransferStatusBySourceID(transferParams["transact"]);
                if (!resultText.Equals("TransferNotFound"))
                    throw new Exception(resultText);
                //проверить параметры
                CheckTransferInParams(transferParams);
                CheckUnistreamServer();
                //ещё что-нибудь

                //
                string senderFirstName = transferParams["senderFirstName"];
                string senderMiddleName = transferParams["senderMiddleName"];
                string senderLastName = transferParams["senderLastName"];
                string recieverFirstName = transferParams["recieverFirstName"];
                string recieverMiddleName = transferParams["recieverMiddleName"];
                string recieverLastName = transferParams["recieverLastName"];
                
                double senderSum = Convert.ToDouble(transferParams["amount"]);
                int country = Convert.ToInt32(transferParams["country"]);
                var sourceID = Convert.ToInt64(transferParams["transact"]);
                next_operation = transferParams["nextOperation"];
                //int senderID = 294278;
                string controlNumber = transferParams["controlNumber"];
                int senderCurr = GetUnistreamCurr(Convert.ToInt32(transferParams["senderCurr"]));
                int recieverCurr = GetUnistreamCurr(Convert.ToInt32(transferParams["recieverCurr"]));

                //создаем клиентов перевода
                var senderConsumer = new Person(); ;// = CreatePerson(senderFirstName, senderMiddleName, senderLastName, transferParams["phoneNumber"]);
                //Console.WriteLine("Номер телефона отправителя = {0}", transferParams["phoneNumber"]);

                if (!FindPerson(senderFirstName, senderMiddleName, senderLastName, transferParams["phoneNumber"], out senderConsumer))
                    senderConsumer = CreatePerson(senderFirstName, senderMiddleName, senderLastName, transferParams["phoneNumber"]);

                // FindPerson(senderFirstName, senderMiddleName, senderLastName, transferParams["phoneNumber"], out senderConsumer);
                var recieverConsumer = CreatePerson(recieverFirstName, recieverMiddleName, recieverLastName);

                //по коду страны получаем участника перевода - банк получатель
                var participatorRecieverBank = GetVirtBankByCountry(country);

                //подготавливаем перевод
                var errorTextPrepareTransfer = "";
                Transfer transfer = EstimateMainAmount(senderSum, senderCurr, recieverCurr, senderID, participatorRecieverBank);
                transfer = PrepareTransfer(senderConsumer, recieverConsumer, senderSum, senderCurr, recieverCurr, senderID, participatorRecieverBank, controlNumber, transfer, out errorTextPrepareTransfer);
                if (transfer == null)
                {
                    throw new Exception(errorTextPrepareTransfer);
                }

                var amounts = transfer.Amounts.ToList();
                /*amounts.ForEach(
                    s => Console.WriteLine("{0} {1} {2}", s.Type, s.Sum, s.CurrencyID)
                    );*/
                double paySum = 0;
                double paySum2 = 0;
                double fee = 0;
                for (int i = 0; i < transfer.Amounts.Length; i++)
                {
                    if (transfer.Amounts[i].Type == AmountType.EstimatedPaidout)
                        paySum = transfer.Amounts[i].Sum;
                    if (transfer.Amounts[i].Type == AmountType.PrimaryPaidComission)
                        fee = transfer.Amounts[i].Sum;
                    if (transfer.Amounts[i].Type == AmountType.ActualPaid)
                        paySum2 = transfer.Amounts[i].Sum;
                    //if (transfer.Amounts[i].Type == AmountType.ActualPaidout)
                    //paySum2 = transfer.Amounts[i].Sum;
                }

                if (Double.Equals(paySum, (double)0))
                    resultText = fee.ToString() + '&' + paySum2.ToString();
                else
                    resultText = fee.ToString() + '&' + paySum.ToString();
                Console.WriteLine(resultText);
                next_operation_final = "kernel_" + next_operation + "_ok";
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                //throw e;
                Console.WriteLine(e.Message);
                //Console.WriteLine(e.Source);
                resultText = e.Message.ToString();
                next_operation_final = "kernel_" + next_operation + "_err";
                result = "1";
            }
            finally
            {
                query = "UPDATE `deltakey`.`exchange` SET `last_operation` = 'check', `next_operation` = '" + next_operation_final + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "', next_try_date = NOW()+INTERVAL 2 SECOND WHERE `exchange`.`transact` = " + transferParams["transact"] + ";";
                ExecQuery(query);
            }
        }

        private static bool FindPerson(string firstName, string middleName, string lastName, string phoneNum, out Person person)
        {
            Boolean result = false;
            person = null;
            WebServiceClient client = new WebServiceClient();
            try
            {
                var request = new FindPersonRequestMessage()
                {
                    AuthenticationHeader = GetCreds(),
                    Firstname = firstName,
                    Middlename = middleName,
                    Lastname = lastName,
                    Phone = phoneNum
                };
                var response = client.FindPerson(request);
                CheckFault(response);
                var persons = response.Persons.ToList();
                Console.WriteLine("Найдено совпадений отправителей {0}", response.Persons.Length);
                if (response.Persons.Length > 0)
                {
                    person = response.Persons[0];
                    result = true;
                }
                return result;
            }
            finally
            {
                if (client.State == System.ServiceModel.CommunicationState.Opened)
                    client.Close();
            }
        }



        //доступность сервера
        //активность сервиса
        private static void CheckUnistreamServer() { }

        private static void CheckTransferInParams(Dictionary<string, string> transferParams)
        {
            if (transferParams.Count != 15)
                throw new Exception("2002");
            CheckFIO(transferParams["senderFirstName"], transferParams["senderMiddleName"], transferParams["senderLastName"]);
            CheckFIO(transferParams["recieverFirstName"], transferParams["recieverMiddleName"], transferParams["recieverLastName"]);
            CheckSum(Convert.ToDouble(transferParams["amount"]));
            CheckCountry(Convert.ToInt32(transferParams["country"]));
            CheckNextOperation(transferParams["nextOperation"]);
            CheckPhoneNumber(transferParams["phoneNumber"]);
        }

        private static void CheckPhoneNumber(string phoneNumber)
        {
            Regex rxNums = new Regex(@"^\d+$"); // любые цифры
            if (phoneNumber.Length < 5 || phoneNumber.Length > 12 || !rxNums.IsMatch(phoneNumber))
                throw new Exception("3003");
        }

        private static void CheckFIO(string surname, string name, string lastName)
        {
            if (surname.Length == 0 || name.Length == 0 || lastName.Length == 0)
                throw new Exception("3003");
        }

        private static void CheckNextOperation(string nextOperation)
        {
            switch (nextOperation)
            {
                case "check":
                case "check_and_die":
                case "pay":
                case "cancel":
                case "cancel_wait":
                case "status":
                case "estimate_main_amount":
                case "return":
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private static void CheckCountry(int country)
        {
            switch (country)
            {
                case 643:
                case 804:
                case 860:
                case 498:
                case 051:
                case 268:
                case 112:
                case 398:
                case 762:
                case 417:
                //Израиль
                case 376:
                //Монголия
                case 496:
                    break;
                default:
                    throw new Exception("no country");
            }
        }

        private static void CheckSum(double sum)
        {
            if (sum <= 0)
            {
                throw new Exception("too little sum");
            }
            if (sum > 100000)
            {
                throw new Exception("too large sum");
            }
        }


        /*
         * метод выполняет запросы INSERT, UPDATE, REPLACE, DELETE
         */
        private static void ExecQuery(string query)
        {
            
            try
            {                
                MySqlCommand myCommand = new MySqlCommand(query, myConnection);
                Console.WriteLine(myCommand.ExecuteNonQuery().ToString());
            }
            finally
            {
                //myConnection.Close();
            }
        }

        /*
        * метод выполняет запросы SELECT
        */
        private static void SelectQuery(string query)
        {
            MySqlConnection myConnection = new MySqlConnection();
            try
            {
                string Connect = "Database=deltakey;Data Source=deltakey.net;User Id=exchange;Password=yj7h4MyR2YUh933x";
                myConnection = new MySqlConnection(Connect);
                myConnection.Open();
                MySqlCommand myCommand = new MySqlCommand(query, myConnection);
                //Console.WriteLine(myCommand.ExecuteNonQuery().ToString());
                MySqlDataReader MyDataReader = myCommand.ExecuteReader();
                /*if (MyDataReader.Read())
                {
                    //MyDataReader.
                }*/
            }
            finally
            {
                myConnection.Close();
            }
        }

        private static Dictionary<String, String> GetTranferParamsDeltakey()
        {
            MySqlDataReader MyDataReader = null;
            string[] strArr = { };
            Dictionary<String, String> remittanceParams = new Dictionary<String, String>();
            string transact = "";
            try
            {
                string CommandText = "SELECT fields, amount, transact, next_operation " +
                                     "FROM  `exchange` " + 
                                     "WHERE  `how` = '102' " +
                                     "  AND `next_operation` IN ("+
                                                                   "'create_transfer'," +
                                                                   "'send_transfer'," +
                                                                   "'check'," +
                                                                   "'estimate_main_amount'," +
                                                                   "'find_person'," +
                                                                   "'check_payout'," +
                                                                   "'check_and_die'," +
                                                                   "'pay'," +
                                                                   "'status'," +
                                                                   "'cancel'," +
                                                                   "'cancel_wait'," +
                                                                   "'return'," +
                                                                   "'payout'" +
                                                                   ") " +
                                     "LIMIT 1 " ;
               
                MySqlCommand myCommand = new MySqlCommand(CommandText, myConnection);
                MyDataReader = myCommand.ExecuteReader();
                if (MyDataReader.Read())
                {
                    string fields = MyDataReader.GetValue(0).ToString();
                    string amount = MyDataReader.GetValue(1).ToString();
                    transact = MyDataReader.GetValue(2).ToString();
                    string next_operation = MyDataReader.GetValue(3).ToString();
                    strArr = fields.Split('&');

                    switch(next_operation)
                    {
                        case "estimate_main_amount":
                            if (strArr.Length != 5)
                                throw new Exception("Неверное число входных параметров");
                            //643&3000.25&810&0&810
                            remittanceParams.Add("transact", transact);
                            remittanceParams.Add("nextOperation", next_operation);
                            remittanceParams.Add("countryPayout", strArr[0].ToString());
                            remittanceParams.Add("sumIn", strArr[1].ToString().Replace('.',','));
                            remittanceParams.Add("sumInCurr", strArr[2].ToString());
                            remittanceParams.Add("sumOut", strArr[3].ToString().Replace('.', ','));
                            remittanceParams.Add("sumOutCurr", strArr[4].ToString());  
                            break;
                        case "payout":
                        case "check_payout":
                            if (strArr.Length != 5)
                                throw new Exception("Неверное число входных параметров");
                            //Новикова&Мария&Викторовна&1234123412341234&810
                            remittanceParams.Add("amount", amount);
                            remittanceParams.Add("transact", transact);
                            remittanceParams.Add("nextOperation", next_operation);
                            remittanceParams.Add("recieverLastName", strArr[0].ToString());
                            remittanceParams.Add("recieverFirstName", strArr[1].ToString());
                            remittanceParams.Add("recieverMiddleName", strArr[2].ToString());
                            remittanceParams.Add("controlNumber", strArr[3].ToString());//1234123412341234
                            remittanceParams.Add("recieverCurr", strArr[4].ToString());//810     
                            break;
                        case "find_person":
                            if (strArr.Length != 5)
                                throw new Exception("Неверное число входных параметров");
                            //Иван&Иванов&Иванович&79160445222&1983-10-10
                            remittanceParams.Add("transact", transact);
                            remittanceParams.Add("nextOperation", next_operation);
                            remittanceParams.Add("senderFirstName", strArr[0].ToString());
                            remittanceParams.Add("senderLastName", strArr[1].ToString());
                            remittanceParams.Add("senderMiddleName", strArr[2].ToString());
                            remittanceParams.Add("phoneNumber", strArr[3].ToString());
                            remittanceParams.Add("dateBirth", strArr[4].ToString());
                            
                            break;
                        case "send_transfer":
                            if (strArr.Length != 29)
                                throw new Exception("Неверное число входных параметров");
                            remittanceParams.Add("transact", transact);
                            remittanceParams.Add("nextOperation", next_operation);
                            remittanceParams.Add("sumIn", strArr[0].ToString().Replace('.', ','));
                            remittanceParams.Add("sumOut", strArr[1].ToString().Replace('.', ','));
                            remittanceParams.Add("recieverCurr", strArr[2].ToString());
                            remittanceParams.Add("country", strArr[3].ToString());
                            remittanceParams.Add("senderLastName", strArr[4].ToString());
                            remittanceParams.Add("senderFirstName", strArr[5].ToString());
                            remittanceParams.Add("senderMiddleName", strArr[6].ToString());
                            remittanceParams.Add("recieverLastName", strArr[7].ToString());
                            remittanceParams.Add("recieverFirstName", strArr[8].ToString());
                            remittanceParams.Add("recieverMiddleName", strArr[9].ToString());
                            remittanceParams.Add("phoneNumber", strArr[10].ToString());
                            remittanceParams.Add("senderCurr", "810");//RUB
                            remittanceParams.Add("controlNumber", strArr[11].ToString());
                            remittanceParams.Add("senderIsResident", strArr[12].ToString());
                            remittanceParams.Add("senderDocumentType", strArr[13].ToString());
                            remittanceParams.Add("senderDocumentSerial", strArr[14].ToString());
                            remittanceParams.Add("senderDocumentNumber", strArr[15].ToString());
                            remittanceParams.Add("senderDocumentDateIssue", strArr[16].ToString());
                            remittanceParams.Add("senderDocumentIssuer", strArr[17].ToString());
                            remittanceParams.Add("senderDocumentDepCode", strArr[18].ToString());
                            remittanceParams.Add("senderDateBirth", strArr[19].ToString());
                            remittanceParams.Add("senderPlaceBirth", strArr[20].ToString());
                            remittanceParams.Add("senderRegCountry", strArr[21].ToString());
                            remittanceParams.Add("senderRegCity", strArr[22].ToString());
                            remittanceParams.Add("senderRegStreet", strArr[23].ToString());
                            remittanceParams.Add("senderRegHouse", strArr[24].ToString());
                            remittanceParams.Add("senderRegBuilding", strArr[25].ToString());
                            remittanceParams.Add("senderRegFlat", strArr[26].ToString());
                            remittanceParams.Add("senderRegIndex", strArr[27].ToString());
                            remittanceParams.Add("recieverIsResident", strArr[28].ToString());

                            break;
                        case "create_transfer":
                            /*
                                0&
                                810&
                                643&
                                Новиков&
                                Андрей&
                                Николаевич&
                                Хатохина&
                                Наталья&
                                Викторовна&
                                +79265400303&
                                201302041402359596&
                                1&
                                Паспорт&
                                4611&
                                172110&
                                2010-12-08&
                                ОТДЕЛОМ УФМС РОССИИ ПО МОСКОВСКОЙ ОБЛ. В ПАВЛОВО-ПОСАДСКОМ Р-НЕ&
                                1969-08-30&
                                ГОР. КЕМЕРОВО&
                                643&
                                гор. Павловский посад&
                                бол жел дор пр.&
                                2&
                                0&
                                186&
                                123456&
                                1    
                             *  'sender_nr_document_type'=>'23',
	                            'sender_nr_document_serial'=>'',
	                            'sender_nr_document_number'=>'2312',
	                            'sender_nr_document_date_issue'=>'2010-12-08',
	                            'sender_nr_document_issuer'=>'Выдан',
	                            'sender_nr_document_dep_code'=>'24563',
	                            'sender_nr_document_date_expire'=>'23'
                             */
                            if (strArr.Length != 36)
                                throw new Exception("Неверное число входных параметров");
                            remittanceParams.Add("transact", transact);
                            remittanceParams.Add("nextOperation", next_operation);
                            remittanceParams.Add("sumIn", strArr[0].ToString().Replace('.', ','));
                            remittanceParams.Add("sumOut", strArr[1].ToString().Replace('.', ','));
                            remittanceParams.Add("recieverCurr", strArr[2].ToString());
                            remittanceParams.Add("country", strArr[3].ToString());
                            remittanceParams.Add("senderLastName", strArr[4].ToString());
                            remittanceParams.Add("senderFirstName", strArr[5].ToString());
                            remittanceParams.Add("senderMiddleName", strArr[6].ToString());
                            remittanceParams.Add("recieverLastName", strArr[7].ToString());
                            remittanceParams.Add("recieverFirstName", strArr[8].ToString());
                            remittanceParams.Add("recieverMiddleName", strArr[9].ToString());
                            remittanceParams.Add("phoneNumber", strArr[10].ToString());
                            remittanceParams.Add("senderCurr", "810");//RUB
                            remittanceParams.Add("controlNumber", strArr[11].ToString());
                            remittanceParams.Add("senderIsResident", strArr[12].ToString());
                            remittanceParams.Add("senderDocumentType", strArr[13].ToString());
                            remittanceParams.Add("senderDocumentSerial", strArr[14].ToString());
                            remittanceParams.Add("senderDocumentNumber", strArr[15].ToString());
                            remittanceParams.Add("senderDocumentDateIssue", strArr[16].ToString());
                            remittanceParams.Add("senderDocumentIssuer", strArr[17].ToString());
                            remittanceParams.Add("senderDocumentDepCode", strArr[18].ToString());
                            remittanceParams.Add("senderDateBirth", strArr[19].ToString());
                            remittanceParams.Add("senderPlaceBirth", strArr[20].ToString());
                            remittanceParams.Add("senderRegCountry", strArr[21].ToString());
                            remittanceParams.Add("senderRegCity", strArr[22].ToString());
                            remittanceParams.Add("senderRegStreet", strArr[23].ToString());
                            remittanceParams.Add("senderRegHouse", strArr[24].ToString());
                            remittanceParams.Add("senderRegBuilding", strArr[25].ToString());
                            remittanceParams.Add("senderRegFlat", strArr[26].ToString());
                            remittanceParams.Add("senderRegIndex", strArr[27].ToString());

                            remittanceParams.Add("recieverIsResident", strArr[28].ToString());

                            remittanceParams.Add("senderNrDocumentType", strArr[29].ToString());
                            remittanceParams.Add("senderNrDocumentSerial", strArr[30].ToString());
                            remittanceParams.Add("senderNrDocumentNumber", strArr[31].ToString());
                            remittanceParams.Add("senderNrDocumentDateIssue", strArr[32].ToString());
                            remittanceParams.Add("senderNrDocumentIssuer", strArr[33].ToString());
                            remittanceParams.Add("senderNrDocumentDepCode", strArr[34].ToString());
                            remittanceParams.Add("senderNrDocumentDateExpire", strArr[35].ToString());
                            
                            break;                       
                        default:
                            if (strArr.Length != 10)
                                throw new Exception("Неверное число входных параметров");
                            //643&Новикова&Мария&Викторовна&Новиков&Андрей&Николаевич&Тест денежного перевода&9265400303&810&1234123412341234
                            remittanceParams.Add("amount", amount);
                            remittanceParams.Add("transact", transact);
                            remittanceParams.Add("nextOperation", next_operation);
                            remittanceParams.Add("country", strArr[0].ToString());
                            remittanceParams.Add("recieverCurr", strArr[1].ToString());//USD                    
                            remittanceParams.Add("senderLastName", strArr[2].ToString());
                            remittanceParams.Add("senderFirstName", strArr[3].ToString());
                            remittanceParams.Add("senderMiddleName", strArr[4].ToString());
                            remittanceParams.Add("recieverLastName", strArr[5].ToString());
                            remittanceParams.Add("recieverFirstName", strArr[6].ToString());
                            remittanceParams.Add("recieverMiddleName", strArr[7].ToString());
                            remittanceParams.Add("comment", "comment");
                            remittanceParams.Add("phoneNumber", strArr[8].ToString());
                            remittanceParams.Add("senderCurr", "810");//RUB
                            remittanceParams.Add("controlNumber", strArr[9].ToString());                            
                            break;
                    }
                }
                MyDataReader.Close();
                return remittanceParams;
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                Console.WriteLine(e.Message);
                MyDataReader.Close();
                if (transact != "")
                {
                    string resultText = e.Message;
                    string nextOperation = "kernel_err";
                    string result = "1";
                    string status = "2";
                    string query = "UPDATE `deltakey`.`exchange` SET `next_operation` = '" + nextOperation + "', result_text = '" + resultText + "', status = '" + status + "', result = '" + result + "' WHERE `exchange`.`transact` = " + transact + ";";
                    ExecQuery(query);
                }
                return remittanceParams;
            }
        }

        private static int GetVirtBankByCountry(int country)
        {
            try
            {
                switch (country)
                {
                    //Россия
                    case 643:
                        return 27;
                    //Узбекистан
                    case 860:
                        return 1313;
                    //Украина
                    case 804:
                        return 133;
                    //Молдова
                    case 498:
                        return 227;
                    //Армения
                    case 051:
                        return 30;
                    //Грузия
                    case 268:
                        return 95;
                    //Беларусь
                    case 112:
                        return 289;
                    //Казахстан
                    case 398:
                        return 572;
                    //Таджикистан
                    case 762:
                        return 1507;
                    //Кигризия
                    case 417:
                        return 481;
                    //Израиль
                    case 376:
                        return 8241;
                    //Монголия
                    case 496:
                        return 2521;
                    default:
                        throw new Exception("3014");
                }
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                throw new Exception("3014");
                //Console.WriteLine(e.Source);
            }
        }
        private static int GetCoutryIDByCountryISO(string country)
        {
            try
            {
                switch (country)
                {
                    //Россия
                    case "643":
                        return 18;
                    //Украина
                    case "804":
                        return 23;
                    default:
                        throw new Exception("3014");
                }
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                throw new Exception("3014");
            }
        }

        private static string GetCoutryISOByCountryID(int country)
        {
            try
            {
                switch (country)
                {
                    //Россия
                    case 18:
                        return "643";
                    //Украина
                    case 23:
                        return "804";
                    default:
                        throw new Exception("3014");
                }
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                throw new Exception("3014");
            }
        }

        private static ArrayList InsertTransfer(Transfer transfer, Int64 sourceID)
        {
            WebServiceClient unistream = new WebServiceClient();
            ArrayList iTR = new ArrayList();
            
            try
            {
                InsertTransferRequestMessage req = new InsertTransferRequestMessage()
                {
                    AuthenticationHeader = GetCreds()
                };
                // собственный идентификатор перевода в системе отправителя
                transfer.SourceID = sourceID.ToString();
                req.Transfer = transfer;
                log.WriteCmd("Запрос", "InsertTransfer");
                log.WriteCmd(Serialize(req), "InsertTransfer");
                // отправим перевод
                var insertTransferResponse = unistream.InsertTransfer(req);
                log.WriteCmd("Ответ", "InsertTransfer");
                log.WriteCmd(Serialize(insertTransferResponse), "InsertTransfer");
                CheckFault(insertTransferResponse);

                Console.WriteLine(
                    "insertTransferResponse: Transfer.ID={0}, resp.Transfer.ControlNumber={1}, resp.Transfer.Status={2}, SourceID={3}",
                    insertTransferResponse.Transfer.ID, insertTransferResponse.Transfer.ControlNumber,
                    insertTransferResponse.Transfer.Status, insertTransferResponse.Transfer.SourceID);

                iTR.Add(insertTransferResponse.Transfer.ID);
                iTR.Add(insertTransferResponse.Transfer.ControlNumber);
                iTR.Add(insertTransferResponse.Transfer.Status);
                iTR.Add(insertTransferResponse.Transfer.SourceID);
                return iTR;

            }
            finally
            {
                if (unistream.State == System.ServiceModel.CommunicationState.Opened)
                    unistream.Close();
            }
        }

        private static Transfer EstimateMainAmount(double totalAmount, int senderCurr, int recieverCurr, int senderID, int participatorRecieverBank)
        {

            try
            {
                WebServiceClient client = new WebServiceClient();
                Transfer transfer = new Transfer();
                
                transfer.Type = TransferType.Remittance;
                transfer.SentDate = DateTime.SpecifyKind(DateTime.Now.Date, DateTimeKind.Unspecified);

                //если валюты совпадают, то перевод в валюте отправителя
                if (recieverCurr == senderCurr)
                // if(true)
                {
                    transfer.Amounts = new Amount[]
                                       {
                                           new Amount() { CurrencyID = senderCurr, Sum = 0, Type = AmountType.Main },
                                       };
                }
                else
                {
                    transfer.Amounts = new Amount[]
                                       {
                                           new Amount() { CurrencyID = senderCurr, Sum = 0, Type = AmountType.ActualPaid },
                                           new Amount() { CurrencyID = recieverCurr, Sum = 0, Type = AmountType.EstimatedPaidout },
                                       };
                }

                transfer.Participators = new Participator[]
                                             {
                                                 new Participator()
                                                     {
                                                         ID = senderID,
                                                         Role = ParticipatorRole.SenderPOS
                                                     },
                                                 new Participator()
                                                     {
                                                         ID = participatorRecieverBank,
                                                         Role = ParticipatorRole.ExpectedReceiverPOS
                                                     }
                                             };
                var req = new EstimateMainAmountRequestMessage()
                {
                    AuthenticationHeader = GetCreds(),
                    Transfer = transfer,
                    TotalAmount = totalAmount
                };
                log.WriteCmd("Запрос", "EstimateMainAmount");
                log.WriteCmd(Serialize(req), "EstimateMainAmount");
                var estimateResult = client.EstimateMainAmount(req);
                log.WriteCmd("Ответ", "EstimateMainAmount");
                log.WriteCmd(Serialize(estimateResult), "EstimateMainAmount");
                CheckFault(estimateResult);
               // Console.WriteLine("амоунты :");
                // выведем комиссии
                /*estimateResult.Transfer.Amounts.ToList().ForEach(
                    a =>
                    Console.WriteLine(
                        @"a.CurrencyID={0}, a.Type={1}, a.Sum={2}",
                        a.CurrencyID, a.Type, a.Sum));

                */
                /*
                Console.WriteLine("Комиссии:");
                // выведем комиссии
                estimateResult.Transfer.Services.ToList().ForEach(
                    s =>
                    Console.WriteLine(
                        @"s.Fee={0}, s.CurrencyID={1}, s.ParticipatorID={2}, s.Mode={3}, s.ServiceID={4}", s.Fee,
                        s.CurrencyID, s.ParticipatorID, s.Mode, s.ServiceID));
                 */
                return estimateResult.Transfer;
            }
            catch (Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                Console.WriteLine("EstimateMainAmount Exception");
                Console.WriteLine(e.Message);
                throw e;
                //return null;
            }
        }

        private static Transfer PrepareTransfer(Person senderConsumer, Person recieverConsumer, double senderSum, int senderCurr, int recieverCurr, int senderID, int participatorRecieverBank, string controlNumber, Transfer transfer, out  string errorTextPrepareTransfer)
        {
            WebServiceClient unistream = new WebServiceClient();
            
            try
            {
                Transfer transfer1 = transfer;
                transfer.Type = transfer1.Type;
                transfer.SentDate = transfer1.SentDate;                
                transfer.ControlNumber = controlNumber;
                errorTextPrepareTransfer = "";
                //если валюты совпадают, то перевод в валюте отправителя
                if (recieverCurr == senderCurr)
                {
                    transfer.Amounts = new Amount[]
                                       {
                                           //transfer1.Amounts[0]
                                           new Amount() {CurrencyID = senderCurr, Sum = transfer.Amounts[0].Sum, Type = AmountType.Main}//
                                           // new Amount() {CurrencyID = senderCurr, Sum = 0, Type = AmountType.ActualPaid} 
                                       };
                }
                else
                {
                    double recieverSum = 0;
                    transfer.Amounts = new Amount[]
                                       {
                                           //transfer.Amounts[0],
                                           new Amount() {CurrencyID = senderCurr, Sum = transfer.Amounts[0].Sum, Type = AmountType.ActualPaid},
                                           new Amount() {CurrencyID = recieverCurr, Sum = recieverSum, Type = AmountType.EstimatedPaidout}                                           
                                       };
                }

                transfer.Participators = transfer1.Participators;
                /* transfer.Participators = new Participator[]
                                              {
                                                  new Participator()
                                                      {
                                                          ID = senderID,
                                                          Role = ParticipatorRole.SenderPOS
                                                      },
                                                  new Participator()
                                                      {
                                                          ID = participatorRecieverBank,
                                                          Role = ParticipatorRole.ExpectedReceiverPOS
                                                      }
                                              };
                 */
                transfer.Consumers = new Consumer[] { };
                transfer.Consumers = new Consumer[]
                                         {
                                             new Consumer()
                                                 {
                                                     Person = senderConsumer,
                                                     Role = ConsumerRole.Sender
                                                 },
                                             new Consumer()
                                                 {
                                                     Person = recieverConsumer,
                                                     Role = ConsumerRole.ExpectedReceiver
                                                 }
                                         };
                PrepareTransferRequestMessage req = new PrepareTransferRequestMessage()
                {
                    AuthenticationHeader = GetCreds(),
                    Transfer = transfer
                };
                log.WriteCmd("Запрос", "PrepareTransfer");
                log.WriteCmd(Serialize(req), "PrepareTransfer");
                var prepareResult = unistream.PrepareTransfer(req);
                CheckFault(prepareResult);                  
                CheckTransferRestriction(prepareResult);
                log.WriteCmd("Ответ", "PrepareTransfer");
                log.WriteCmd(Serialize(prepareResult), "PrepareTransfer");
                //Console.WriteLine(Serialize(prepareResult));
                //CheckFault(prepareResult);
                transfer.Amounts = prepareResult.Transfer.Amounts;
                var amounts = transfer.Amounts;
                var services = prepareResult.Transfer.Services.ToList();
                //Console.WriteLine("Transfer.Comment={0}", prepareResult.Transfer.Comment);
                transfer.Comment = prepareResult.Transfer.Comment;
                Console.WriteLine("prepareResult.Transfer.ControlNumber={0}", prepareResult.Transfer.ControlNumber);
                transfer.ControlNumber = prepareResult.Transfer.ControlNumber;
                // выведем комиссии
                services.ForEach(
                    s =>
                    Console.WriteLine(
                        @"s.Fee={0}, s.CurrencyID={1}, s.ParticipatorID={2}, s.Mode={3}, s.ServiceID={4}", s.Fee,
                        s.CurrencyID, s.ParticipatorID, s.Mode, s.ServiceID));
                services.ForEach(
                    s => s.Response = s.Mode == ServiceMode.Required ? Response.Accepted : Response.Rejected);

                double feeSum = 0;
                services.ForEach(
                        s => feeSum += s.Fee
                    );
                Console.WriteLine("Common fee {0}", feeSum);

                transfer.Services = services.ToArray();

                /*transfer.Amounts.ToList().ForEach(
                    s =>
                    Console.WriteLine(
                        @"s.Type={0}, s.CurrencyID={1}, s.Sum={2}", s.Type,
                        s.CurrencyID, s.Sum));
                 * */

                //
                if (senderCurr == recieverCurr)
                {
                    transfer.Amounts = new Amount[]
                                       {    transfer.Amounts[0],
                                           new Amount()
                                               {
                                                   CurrencyID = senderCurr, 
                                                   Sum = transfer.Amounts[0].Sum, 
                                                   Type = AmountType.ActualPaid
                                               },                           
                                           new Amount()
                                               {
                                                   CurrencyID = senderCurr,
                                                   Sum = feeSum,
                                                   Type = AmountType.PrimaryPaidComission
                                               }
                                       };
                }
                /*  else
                  {
                      Array.Resize(ref amounts, transfer.Amounts.Length + 1);
                      amounts[amounts.Length - 1] = 
                                             new Amount()
                                                 {
                                                     CurrencyID = senderCurr,
                                                     Sum = feeSum,
                                                     Type = AmountType.PrimaryPaidComission
                                                 };

                      Amount am = new Amount()
                                      {
                                          CurrencyID = senderCurr,
                                          Sum = feeSum,
                                          Type = AmountType.PrimaryPaidComission
                                      };
                      amounts[3] = am;
                      transfer.Amounts = amounts;
                  }*/

                transfer.CashierUserAction = new UserActionInfo()
                {
                    ActionLocalDateTime = DateTime.Now.Clarify2(),
                    UserID = 1,
                    UserUnistreamCard = "hello1"
                };

                transfer.TellerUserAction = new UserActionInfo()
                {
                    ActionLocalDateTime = DateTime.Now.Clarify2(),
                    UserID = 2,
                    UserUnistreamCard = "hello2"
                };
                return transfer;
            }
            catch (System.Exception e)
            {
                log.WriteCmd(e.Message, "CatchExceptions");
                Console.WriteLine(e.Message);
                Console.WriteLine("PrepareTransfer Exception");
                errorTextPrepareTransfer = e.Message;
                return null;
            }
            finally
            {
                if (unistream.State == System.ServiceModel.CommunicationState.Opened)
                    unistream.Close();
            }
        }

        private static Person CreatePerson(string firstName, string middleName, string lastName)
        {
            WebServiceClient client = new WebServiceClient();
            try
            {
                Person person = new Person();
                person.FirstName = firstName;
                person.MiddleName = middleName;
                person.LastName = lastName;
                var request = new CreatePersonRequestMessage()
                {
                    AuthenticationHeader = GetCreds(),
                    Person = person
                };
                var response = client.CreatePerson(request);
                CheckFault(response);
                person = response.Person;
                Console.WriteLine(@" {0} {1} {2} {3} / {4} {5} {6}", person.ID, person.FirstName, person.MiddleName, person.LastName, person.FirstNameLat, person.MiddleNameLat, person.LastNameLat);
                return person;
            }
            finally
            {
                if (client.State == System.ServiceModel.CommunicationState.Opened)
                    client.Close();
            }
        }

        private static Person CreatePerson(string firstName, string middleName, string lastName, string phoneNum)
        {
            WebServiceClient client = new WebServiceClient();
            try
            {
                Person person = new Person();
                person.FirstName = firstName;
                person.MiddleName = middleName;
                person.LastName = lastName;
                Phone persPhone = new Phone();
                persPhone.Type = PhoneType.Mobile;
                persPhone.CountryID = 18;//Россия
                persPhone.AreaCode = phoneNum.Substring(0, 3);
                persPhone.Number = phoneNum.Substring(3);
                Phone[] phoneArr = { persPhone };
                person.Phones = phoneArr;
                var request = new CreatePersonRequestMessage()
                {
                    AuthenticationHeader = GetCreds(),
                    Person = person
                };
                var response = client.CreatePerson(request);
                CheckFault(response);
                person = response.Person;
                Console.WriteLine(@" {0} {1} {2} {3} / {4} {5} {6}", person.ID, person.FirstName, person.MiddleName, person.LastName, person.FirstNameLat, person.MiddleNameLat, person.LastNameLat);
                return person;
            }
            finally
            {
                if (client.State == System.ServiceModel.CommunicationState.Opened)
                    client.Close();
            }
        }

        private static GetCountriesChangesResponseMessage GetCountriesChanges()
        {
            WebServiceClient client = new WebServiceClient();
            var request = new GetCountriesChangesRequestMessage()
            {
                AuthenticationHeader = GetCreds()
            };
            var response = client.GetCountriesChanges(request);
            CheckFault(response);
            return response;
        }

        private static GetRegionsChangesResponseMessage GetRegionsChanges()
        {
            WebServiceClient client = new WebServiceClient();
            var request = new GetRegionsChangesRequestMessage()
            {
                AuthenticationHeader = GetCreds()
            };
            var response = client.GetRegionsChanges(request);
            CheckFault(response);
            return response;
        }

        private static GetBanksChangesResponseMessage GetBanksChanges()
        {
            WebServiceClient client = new WebServiceClient();
            var request = new GetBanksChangesRequestMessage()
            {
                AuthenticationHeader = GetCreds()
            };
            var response = client.GetBanksChanges(request);
            CheckFault(response);
            return response;
        }

        private static GetCurrenciesChangesResponseMessage GetCurrenciesChanges()
        {
            WebServiceClient client = new WebServiceClient();
            var request = new GetCurrenciesChangesRequestMessage()
            {
                AuthenticationHeader = GetCreds()
            };
            var response = client.GetCurrenciesChanges(request);
            CheckFault(response);
            return response;
        }

        private static GetDocumentTypeChangesResponseMessage GetDocumentTypeChanges()
        {
            WebServiceClient client = new WebServiceClient();
            var request = new GetDocumentTypeChangesRequestMessage()
            {
                AuthenticationHeader = GetCreds()
            };
            var response = client.GetDocumentTypeChanges(request);
            CheckFault(response);
            return response;
        }

        /* private static ServiceReference.AuthenticationHeader GetCreds()
         {
             return new ServiceReference.AuthenticationHeader()
             {
                 AppKey = "1wwteyFGFew624",  // valid for test environment only
                 Username = "deltakey",  // ask unistream
                 Password = "nsv9A9Pvx",
             };
         }*/
        private static ServiceReference1.AuthenticationHeader GetCreds()
        {
            return new ServiceReference1.AuthenticationHeader()
            {
                AppKey = "1wwteyFGFew624",  // valid for test environment only
                Username = "deltakey",  // ask unistream
                Password = "3MXs7fdb",

                /*
                 
                 
                 */
            };
        }

        public static void CheckFault(WsResponse response)
        {
            //try
            //{
                // YOU MUST PERFORM AT LEAST THE FOLLOWING CHECK FOR EACH RESPONSE TAKEN FROM WEB SERVICE Global 2 
                if (response.Fault != null)
                {
                    
                    log.WriteCmd(Serialize(response), "Exceptions");
                    throw new System.Exception(
                        string.Format("Unistream error. Code={0}, ID={1}, Msg={2}",
                                       response.Fault.Code,
                                       response.Fault.ID,
                                       response.Fault.Message)
                                       );
                         /*
                    string.Format("{0}",                                      
                                       response.Fault.Message)
                                                               );*/
                }
           // }
            /*catch (System.Exception e)
            {
                Console.WriteLine(e.Message);
            }
             * */
        }

        public static void CheckTransferRestriction(PrepareTransferResponseMessage response)
        {
            // YOU MUST PERFORM AT LEAST THE FOLLOWING CHECK FOR EACH RESPONSE TAKEN FROM WEB SERVICE Global 2 
            if (response.TransferRestrictions != null)
            {
                Console.WriteLine("restrictions count = {0}", response.TransferRestrictions.Count());
                if (response.TransferRestrictions.Count() != 0)
                {
                    throw new ApplicationException(
                        string.Format("Unistream Check Transfer Restriction error. Type={0}, ID={1}, Quantity={2}, Msg={3}",
                                      response.TransferRestrictions[0].Type.ToString(),
                                      response.TransferRestrictions[0].ID,
                                      response.TransferRestrictions[0].Quantity,
                                      response.TransferRestrictions[0].Message[0].Text
                                      ));
                }
            }
        }


        public static string RetCheckFault(WsResponse response)
        {
            // YOU MUST PERFORM AT LEAST THE FOLLOWING CHECK FOR EACH RESPONSE TAKEN FROM WEB SERVICE Global 2 
            if (response.Fault != null)
            {
                
                log.WriteCmd(Serialize(response), "Exceptions");
                /*throw new ApplicationException(
                    string.Format("Unistream error. Code={0}, ID={1}, Msg={2}",
                                  response.Fault.Code,
                                  response.Fault.ID,
                                  response.Fault.Message));
                 * */


                return response.Fault.Code.ToString();
            }
            return "OK";
        }

        private static string Serialize(object obj)
        {
            if (obj == null)
                return "null";

            XmlSerializer x = new XmlSerializer(obj.GetType());
            byte[] b;

            using (MemoryStream ms = new MemoryStream())
            {
                x.Serialize(ms, obj);
                b = ms.ToArray();
            }

            var str = Encoding.UTF8.GetString(b);

            return str;
        }

    }

    public static class Extensions
    {
        /// <summary>
        /// Gets the date component with unspecified kind.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static DateTime Clarify(this DateTime source)
        {
            return DateTime.SpecifyKind(source.Date, DateTimeKind.Unspecified);
        }

        /// <summary>
        /// Gets the datetime with unspecified kind.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static DateTime Clarify2(this DateTime source)
        {
            return DateTime.SpecifyKind(source, DateTimeKind.Unspecified);
        }

    }
}