using RestSharp;
using RestSharp.Authenticators;

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ShoperTrak
{
    class Program
    {
        static void Main(string[] args)
        {
            string date = args[0];

            //gets data from api
            hierarchies storeData = getStoreData();

            //stores data in memory as objects
            List<Store> stores = getStores(storeData);
           
            //for each store gets all trafic data for given data
            storeTrafficData(date, stores);
           

        }

        private static void storeTrafficData(string date, List<Store> stores)
        {

            var client = new RestClient("https://stws.shoppertrak.com")
            {
                Authenticator = new HttpBasicAuthenticator("censored", "censored") //basic authentication
            };


            var request = new RestRequest("KeyPerformanceIndicators/v1.0/service/hierarchy", Method.GET);


            // add HTTP Headers
            request.AddHeader("header", "value");


            // execute the request
            IRestResponse response = client.Execute(request);
            string content = response.Content; // raw content as string
            XmlSerializer serializer = new XmlSerializer(typeof(hierarchies));

            foreach (Store s in stores)
            {
                try
                {
                    request = new RestRequest("KeyPerformanceIndicators/v1.0/service/hourlyPerformance/{stId}?date={date}", Method.GET);
                    Console.WriteLine(s.name);
                    request.AddUrlSegment("stId", s.shopperTrakId.ToString()); // replaces matching token in request {stId}
                    request.AddUrlSegment("date", date); // replaces matching token in request {date}
                    response = client.Execute(request);
                    content = response.Content; // raw content as string
                    serializer = new XmlSerializer(typeof(kpis));
                    kpis root = null;

                    using (TextReader reader = new StringReader(content))
                    {
                        root = (kpis)serializer.Deserialize(reader);
                    }
                    foreach (kpisHierarchyNodeHour h in root.hierarchyNode.hour)
                    {
                        try
                        {
                            hour temp = new hour { start = h.startDateTime, exit = h.traffic.exits, enters = h.traffic.enters };
                            s.hours.Add(temp);
                            Console.WriteLine("\t" + temp.getStartHour() + "\t" + h.traffic.enters + "\t" + h.traffic.exits);

                            using (SqlConnection sqlConnection1 = new SqlConnection("Data Source = 192.168.1.250; Integrated Security = False; User ID = KIOSKAPP; Password=MtCr!R1 ;Connect Timeout = 15; Encrypt=False;TrustServerCertificate=True;ApplicationIntent=ReadWrite;MultiSubnetFailover=False"))
                            {

                                using (SqlCommand command = new SqlCommand("DW.dbo.sp_StoreTraffic_Update", sqlConnection1))
                                {
                                    command.CommandType = CommandType.StoredProcedure;
                                    command.Parameters.AddWithValue("@LocationId", s.id);
                                    command.Parameters.AddWithValue("@TrafficDate", date);
                                    command.Parameters.AddWithValue("@TrafficTime", temp.getStartHour());
                                    command.Parameters.AddWithValue("@LocationCode ", s.code);
                                    command.Parameters.AddWithValue("@InCount ", temp.enters);
                                    command.Parameters.AddWithValue("@OutCount ", temp.exit);
                                    command.Parameters.AddWithValue("@TrafficCount  ", temp.exit);

                                    sqlConnection1.Open();
                                    int result = command.ExecuteNonQuery();

                                    // Check Error
                                    if (result < 0)
                                        Console.WriteLine("Error inserting data into Database!");
                                }


                            }

                        }
                        catch
                        {
                            Console.WriteLine("Error adding record for date: " + date + "hour: " + h.startDateTime + "store: " + s.id);
                        }

                    }

                    Thread.Sleep(2000); //had to slow down due to api request per second limit.
                }
                catch
                {
                    Console.WriteLine("Error adding record for store: " + s.code + " on date: " + date);
                }
            }
            Console.ReadLine();
        }

        private static List<Store> getStores(hierarchies storeData)
        {



            List<Store> stores = new List<Store>();

            foreach (hierarchiesHierarchy h in storeData.hierarchy)
            {

                foreach (hierarchiesHierarchyHierarchyNode hn in h.hierarchyNode)
                {
                    if (hn.parentID == 87740001)
                    {
                        Store temp = new Store { code = hn.customerID, shopperTrakId = hn.shopperTrakID, name = hn.name };
                        using (SqlConnection sqlConnection1 = new SqlConnection("Data Source = 192.168.1.250; Integrated Security = False; User ID = censored; Password=censored ;Connect Timeout = 15; Encrypt=False;TrustServerCertificate=True;ApplicationIntent=ReadWrite;MultiSubnetFailover=False"))
                        {

                            SqlCommand cmd = new SqlCommand();
                            SqlDataReader reader;

                            cmd.CommandText = @"SELECT [location_id]      
                                                ,[location_code]
                                                FROM[EPICORSQL].[me_jkl_01].[dbo].[location] where location_code =" + temp.code;
                            cmd.Connection = sqlConnection1;

                            sqlConnection1.Open();

                            reader = cmd.ExecuteReader();

                            while (reader.Read())
                            {
                                temp.id = reader.GetValue(0).ToString();


                            }
                            reader.Close();

                        }

                        stores.Add(temp);
                    }
                }
            }

            return stores;
        }

        public static hierarchies getStoreData()
        {
            var client = new RestClient("https://stws.shoppertrak.com")
            {
                Authenticator = new HttpBasicAuthenticator("jakoapi", "jako123") //basic authentication
            };


            var request = new RestRequest("KeyPerformanceIndicators/v1.0/service/hierarchy", Method.GET);


            // add HTTP Headers
            request.AddHeader("header", "value");


            // execute the request
            IRestResponse response = client.Execute(request);
            string content = response.Content; // raw content as string
            XmlSerializer serializer = new XmlSerializer(typeof(hierarchies));
            hierarchies obj;
            using (TextReader reader = new StringReader(content))
            {
                obj = (hierarchies)serializer.Deserialize(reader);
            }
            return obj;
        }

    }

    internal class Store
    {
        public string id;
        public string code;
        public string name;
        public uint shopperTrakId;
        public List<hour> hours = new List<hour>();
    }

    internal class hour
    {
        public ulong start = 0;
        public byte enters;
        public byte exit;

        public string getStartHour()
        {
            string result = (((start / 10000) % 100) * 100).ToString();
            if (result.Length < 4)
            {
                result = "0" + result;
            }
            return result;
        }
    }



    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false)]
    public partial class hierarchies
    {

        private hierarchiesHierarchy[] hierarchyField;

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("hierarchy")]
        public hierarchiesHierarchy[] hierarchy
        {
            get
            {
                return this.hierarchyField;
            }
            set
            {
                this.hierarchyField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class hierarchiesHierarchy
    {

        private string hierarchyNameField;

        private hierarchiesHierarchyHierarchyNode[] hierarchyNodeField;

        private uint shopperTrakIDField;

        /// <remarks/>
        public string hierarchyName
        {
            get
            {
                return this.hierarchyNameField;
            }
            set
            {
                this.hierarchyNameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("hierarchyNode")]
        public hierarchiesHierarchyHierarchyNode[] hierarchyNode
        {
            get
            {
                return this.hierarchyNodeField;
            }
            set
            {
                this.hierarchyNodeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public uint shopperTrakID
        {
            get
            {
                return this.shopperTrakIDField;
            }
            set
            {
                this.shopperTrakIDField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class hierarchiesHierarchyHierarchyNode
    {

        private string customerIDField;

        private string nameField;

        private uint parentIDField;

        private uint shopperTrakIDField;

        /// <remarks/>
        public string customerID
        {
            get
            {
                return this.customerIDField;
            }
            set
            {
                this.customerIDField = value;
            }
        }

        /// <remarks/>
        public string name
        {
            get
            {
                return this.nameField;
            }
            set
            {
                this.nameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public uint parentID
        {
            get
            {
                return this.parentIDField;
            }
            set
            {
                this.parentIDField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public uint shopperTrakID
        {
            get
            {
                return this.shopperTrakIDField;
            }
            set
            {
                this.shopperTrakIDField = value;
            }
        }
    }


    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    [System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false)]
    public partial class kpis
    {

        private kpisHierarchyNode hierarchyNodeField;

        /// <remarks/>
        public kpisHierarchyNode hierarchyNode
        {
            get
            {
                return this.hierarchyNodeField;
            }
            set
            {
                this.hierarchyNodeField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class kpisHierarchyNode
    {

        private ushort customerIDField;

        private string nameField;

        private kpisHierarchyNodeHour[] hourField;

        private kpisHierarchyNodeTotal totalField;

        private uint shopperTrakIDField;

        /// <remarks/>
        public ushort customerID
        {
            get
            {
                return this.customerIDField;
            }
            set
            {
                this.customerIDField = value;
            }
        }

        /// <remarks/>
        public string name
        {
            get
            {
                return this.nameField;
            }
            set
            {
                this.nameField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlElementAttribute("hour")]
        public kpisHierarchyNodeHour[] hour
        {
            get
            {
                return this.hourField;
            }
            set
            {
                this.hourField = value;
            }
        }

        /// <remarks/>
        public kpisHierarchyNodeTotal total
        {
            get
            {
                return this.totalField;
            }
            set
            {
                this.totalField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public uint shopperTrakID
        {
            get
            {
                return this.shopperTrakIDField;
            }
            set
            {
                this.shopperTrakIDField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class kpisHierarchyNodeHour
    {

        private kpisHierarchyNodeHourTraffic trafficField;

        private decimal conversionField;

        private decimal starField;

        private decimal salesField;

        private decimal salesPerShopperField;

        private decimal avgTransactionSizeField;

        private ulong startDateTimeField;

        /// <remarks/>
        public kpisHierarchyNodeHourTraffic traffic
        {
            get
            {
                return this.trafficField;
            }
            set
            {
                this.trafficField = value;
            }
        }

        /// <remarks/>
        public decimal conversion
        {
            get
            {
                return this.conversionField;
            }
            set
            {
                this.conversionField = value;
            }
        }

        /// <remarks/>
        public decimal star
        {
            get
            {
                return this.starField;
            }
            set
            {
                this.starField = value;
            }
        }

        /// <remarks/>
        public decimal sales
        {
            get
            {
                return this.salesField;
            }
            set
            {
                this.salesField = value;
            }
        }

        /// <remarks/>
        public decimal salesPerShopper
        {
            get
            {
                return this.salesPerShopperField;
            }
            set
            {
                this.salesPerShopperField = value;
            }
        }

        /// <remarks/>
        public decimal avgTransactionSize
        {
            get
            {
                return this.avgTransactionSizeField;
            }
            set
            {
                this.avgTransactionSizeField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public ulong startDateTime
        {
            get
            {
                return this.startDateTimeField;
            }
            set
            {
                this.startDateTimeField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class kpisHierarchyNodeHourTraffic
    {

        private byte exitsField;

        private byte entersField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public byte exits
        {
            get
            {
                return this.exitsField;
            }
            set
            {
                this.exitsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public byte enters
        {
            get
            {
                return this.entersField;
            }
            set
            {
                this.entersField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class kpisHierarchyNodeTotal
    {

        private kpisHierarchyNodeTotalTraffic trafficField;

        private decimal conversionField;

        private decimal starField;

        private decimal salesField;

        private decimal salesPerShopperField;

        private decimal avgTransactionSizeField;

        /// <remarks/>
        public kpisHierarchyNodeTotalTraffic traffic
        {
            get
            {
                return this.trafficField;
            }
            set
            {
                this.trafficField = value;
            }
        }

        /// <remarks/>
        public decimal conversion
        {
            get
            {
                return this.conversionField;
            }
            set
            {
                this.conversionField = value;
            }
        }

        /// <remarks/>
        public decimal star
        {
            get
            {
                return this.starField;
            }
            set
            {
                this.starField = value;
            }
        }

        /// <remarks/>
        public decimal sales
        {
            get
            {
                return this.salesField;
            }
            set
            {
                this.salesField = value;
            }
        }

        /// <remarks/>
        public decimal salesPerShopper
        {
            get
            {
                return this.salesPerShopperField;
            }
            set
            {
                this.salesPerShopperField = value;
            }
        }

        /// <remarks/>
        public decimal avgTransactionSize
        {
            get
            {
                return this.avgTransactionSizeField;
            }
            set
            {
                this.avgTransactionSizeField = value;
            }
        }
    }

    /// <remarks/>
    [System.SerializableAttribute()]
    [System.ComponentModel.DesignerCategoryAttribute("code")]
    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
    public partial class kpisHierarchyNodeTotalTraffic
    {

        private ushort exitsField;

        private ushort entersField;

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public ushort exits
        {
            get
            {
                return this.exitsField;
            }
            set
            {
                this.exitsField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public ushort enters
        {
            get
            {
                return this.entersField;
            }
            set
            {
                this.entersField = value;
            }
        }
    }




}
