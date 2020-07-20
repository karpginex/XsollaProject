using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.Encodings;
using System.Text;

namespace XsollaProject
{
    class Program
    {
        public static string type = "payment";
        public static HttpListener listener;
        public static string url = "http://localhost:8080/";


        public class Session
        {
            public static List<Session> list = new List<Session>();
            public Guid sessionId;
            public double amount;
            public string purpose;

            public Session(double amount, string purpose)
            {
                this.amount = amount;
                this.purpose = purpose;
                this.sessionId = Guid.NewGuid();
                list.Add(this);
            }

            public static Session Find (Guid sessionId)
            {
                return list.Find((x) =>
                {
                    if (x.sessionId == sessionId)
                    {
                        return true;
                    }
                    return false;
                });
            }
        }

        public class Data
        {  
            [JsonProperty("type")]
            public string type;
            [JsonProperty("id")]
            public string id;
            [JsonProperty("attributes")]
            public Card attributes;

            public Data(string type, string id)
            {
                this.type = type;
                this.id = id;
                this.attributes = null;
            }

            public Data(string type, string id, Card card)
            {
                this.type = type;
                this.id = id;
                this.attributes = card;
            }
        }

        public class Card
        {
            [JsonProperty("number")]
            public string number;
            [JsonProperty("CVV_CVC")]
            public int CVV_CVC;
            [JsonProperty("year")]
            public int year;
            [JsonProperty("mounth")]
            public int mounth;
            [JsonProperty("URL")]
            public string URL;

            public Card(string number, int CVV_CVC, int year, int mounth, string URL)
            {
                this.number = number;
                this.CVV_CVC = CVV_CVC;
                this.year = year;
                this.mounth = mounth;
                this.URL = URL;
            }
        }


        static bool Luna (string num)
        {
            int parity = num.Length % 2;
            int sum = 0;
            for (int i = 0; i < num.Length; i++)
            {
                int n = int.Parse(num[i].ToString());
                if (i % 2 == parity)
                {
                    n *= 2;
                    n = n > 9 ? n - 9 : n;
                    sum += n;
                }
                else
                {
                    sum += n;
                }
            }
            return sum % 10 == 0;
        }

        public static async Task HandleIncomingConnections()
        {
            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                HttpListenerRequest requesT = context.Request;
                HttpListenerResponse response = context.Response;

                int status = 200;
                string statusDescription = "OK";
                string json = "";

                Error[] errors = new Error[1];

                if (requesT.Url.AbsolutePath == "/" + type)
                {
                    //OK
                    if (requesT.HttpMethod == "GET")
                    {   //Return sessionid
                        try
                        {
                            //Create new payment session
                            Session session = new Session(Double.Parse(requesT.QueryString["amount"]),
                                requesT.QueryString["purpose"]);
                            json = JsonConvert.SerializeObject(new Data(type,
                                session.sessionId.ToString()));
                        }
                        catch (Exception e)
                        {
                            //Error: amount or purpose is not exists
                            errors[0] = new Error(400, "Amount or purpose is not exists!", 6);
                            json = JsonConvert.SerializeObject(errors);
                            status = 400;
                            statusDescription = "Bad Request";
                            Console.WriteLine(e);
                        }
                    }
                    else if (requesT.HttpMethod == "POST")
                    {   //Make payment
                        try
                        {
                            //Read request body
                            dynamic request = JsonConvert.DeserializeObject(
                                ReadInputStream(requesT.InputStream));

                            if (request.type == type)
                            {
                                Session session = Session.Find(Guid.Parse((string)request.id));
                                if (session != null)
                                {
                                    Card card = new Card((string)request.attributes.number, (int)request.attributes.CVV_CVC,
                                        (int)request.attributes.year, (int)request.attributes.mounth,
                                        (string)request.attributes.URL);

                                    
                                    if (card.number.Length >= 4 && card.CVV_CVC >= 100 && card.CVV_CVC <= 999 &&
                                        card.mounth >= 1 && card.year >= 0 && card.URL != "")
                                    {
                                        //Check the card number by Luna algorithm
                                        if (Luna(card.number))
                                        {

                                            //Payment successful
                                            json = JsonConvert.SerializeObject(new Data(type,
                                                session.sessionId.ToString()));


                                        }
                                        else
                                        {
                                            //Error: the card number is not correct
                                            errors[0] = new Error(400, "The card number is not correct", 1);
                                            json = JsonConvert.SerializeObject(errors);
                                            status = 400;
                                            statusDescription = "Bad Request";
                                        }
                                    }
                                    else
                                    {
                                        //Error: the card data is not correct
                                        errors[0] = new Error(400, "The card data is not correct!", 2);
                                        json = JsonConvert.SerializeObject(errors);
                                        status = 400;
                                        statusDescription = "Bad Request";
                                    }

                                    //Delete used session
                                    Session.list.Remove(session);
                                }
                                else
                                {
                                    //Error: dead session
                                    errors[0] = new Error(400, "Dead session!", 3);
                                    json = JsonConvert.SerializeObject(errors);
                                    status = 400;
                                    statusDescription = "Bad Request";
                                }
                            }
                            else
                            {
                                //Error: unknown request type
                                errors[0] = new Error(400, "Unknown request type!", 4);
                                json = JsonConvert.SerializeObject(errors);
                                status = 400;
                                statusDescription = "Bad Request";
                            }
                        }
                        catch (Exception e)
                        {
                            if (e.GetType().Name == "UriFormatException")
                            {
                                errors[0] = new Error(400, "Invalid URL!", 4);
                                json = JsonConvert.SerializeObject(errors);
                                status = 400;
                                statusDescription = "Bad Request";
                            }
                            else if (e.GetType().Name == "FormatException")
                            {
                                errors[0] = new Error(400, "Bad Request", 7);
                                json = JsonConvert.SerializeObject(errors);
                                status = 400;
                                statusDescription = "Bad Request";
                            }
                            else
                            {
                                //Error: 500 - Internal Server Error
                                status = 500;
                                statusDescription = "Internal Server Error";
                                Console.WriteLine(e);
                            }
                        }
                    }
                    else
                    {
                        //Error: unknown method
                        errors[0] = new Error(400, "Unknown method!", 5);
                        json = JsonConvert.SerializeObject(errors);
                        status = 400;
                        statusDescription = "Bad Request";
                    }
                }
                else
                {
                    //Error: resource not found
                    status = 404;
                    statusDescription = "Not Found";
                }

                byte[] data = Encoding.UTF8.GetBytes(json);
                response.StatusCode = status;
                response.StatusDescription = statusDescription;
                response.ContentType = "application/vnd.api+json";
                response.ContentEncoding = Encoding.UTF8;
                response.ContentLength64 = data.LongLength;

                // Write out to the response stream (asynchronously), then close it
                await response.OutputStream.WriteAsync(data, 0, data.Length);
                response.Close();
            }
        }


        public static string ReadInputStream(Stream Request)
        {
            byte[] data = new byte[1024];
            Request.Read(data, 0, 1024);
            string converted = Encoding.UTF8.GetString(data, 0, data.Length);
            return converted;
        }
        public class Error
        { 
            public int status;
            public string title;
            public int code;

            public Error(int status, string title, int code)
            {
                this.status = status;
                this.title = title;
                this.code = code;
            }
        }

        static void Main(string[] args)
        {
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();

            Console.WriteLine("Listening...");

            Task listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();

            listener.Close();

            
        }
    }
}
