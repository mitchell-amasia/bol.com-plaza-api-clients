﻿using System;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using bol.com.PlazaAPI.Helpers;
using System.Collections.Generic;

namespace bol.com.PlazaAPI
{
    /// <summary>
    /// This is the main class and provides the functionallity to connect with the REST services of the Plaza API from bol.com
    /// It also provides the signing request and the response output in JSON format.
    /// </summary>
    public class PlazaAPIClient
    {
        #region Members

        /// <summary>
        /// The public key.
        /// </summary>
        private string _publicKey;

        /// <summary>
        /// The private key.
        /// </summary>
        private string _privateKey;

        /// <summary>
        /// The URL.
        /// </summary>
        private string _url;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PlazaAPIClient"/> class.
        /// To use the <see cref="PlazaAPIClient"/> class, a user will first need to have a valid seller account.
        /// The public/private key combination are provided by bol.com and can be requested through the developer portal at http://developers.bol.com/
        /// Each user will receive their own public/private key combination. 
        /// This scheme is similar to the public/private key principle in PKI (Public Key Infrastructure).
        /// The private key is used to sign the message, while only the public key is passed when communicating with the Plaza API.
        /// NOTE: The public/private key is NOT the same as the ones from bol.com's OpenAPI.
        /// </summary>
        /// <param name="publicKey">The public key.</param>
        /// <param name="privateKey">The private key.</param>
        /// <param name="url">The URL where the REST services are located.</param>
        public PlazaAPIClient(string publicKey, string privateKey, string url)
        {
            if (publicKey == string.Empty)
            {
                throw new Exception("The Public Key cannot be empty.");
            }

            if (privateKey == string.Empty)
            {
                throw new Exception("The Private Key cannot be empty.");
            }

            if (url == string.Empty)
            {
                throw new Exception("The URL cannot be empty.");
            }

            _publicKey = publicKey;
            _privateKey = privateKey;
            
            if (url.EndsWith("/"))
            {
                url = url.Remove(url.LastIndexOf("/"));
            }

            _url = url;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the signing request.
        /// </summary>
        /// <value>
        /// The signing request.
        /// </value>
        public string SigningRequest { get; set; }

        /// <summary>
        /// Gets or sets the response output.
        /// </summary>
        /// <value>
        /// The response.
        /// </value>
        public string ResponseOutput { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// This method gets all currently open orders.
        /// NOTE: An order that has not been shipped is new and thus open.
        /// When marking an order as shipped or cancelled through the API this actually means the package has been shipped and is underway to the customer.
        /// There is no intermediate status that indicates the order is "Acknowledged and in progress"
        /// </summary>
        /// <returns>
        /// An OpenOrders object. This OpenOrders object was generated by a tool.
        /// The different elements are described on the following XML Schema Definition https://plazaapi.bol.com/services/xsd/plazaapiservice-v1.xsd
        /// </returns>
        public OpenOrders GetOrders()
        {
            HttpWebResponse response = null;
            OpenOrders results = null;

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_url + "/services/rest/orders/v1/open/");

                Utils.HandleRequest(request, C.RequestMethods.GET, _publicKey, _privateKey);

                SigningRequest = Utils.StringToSign.Replace("\n", Environment.NewLine);
                response = (HttpWebResponse)request.GetResponse();

                if (HttpStatusCode.OK == response.StatusCode)
                {
                    XmlSerializer ser = new XmlSerializer(typeof(OpenOrders));
                    object obj = ser.Deserialize(response.GetResponseStream());
                    results = (OpenOrders)obj;

                    var json = new JavaScriptSerializer().Serialize(obj);
                    ResponseOutput = json;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    throw ExceptionHandler.HandleResponseException((HttpWebResponse)ex.Response);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }

            return results;
        }

        /// <summary>
        /// Process 1 or more shipping/cancellation notifications to bol.com
        /// The maximum number of shipments/cancellations in one request is 400.
        /// NOTE: After informing bol.com of shippings / cancellations through the API you may still see your orders as "open" in the seller dashboard.
        /// This has to do with system caches and queues.
        /// The API briefly caches the input it receives and the seller dashboard also has it's own cache to offload the core systems. 
        /// The most important feedback is the feedback one gets from the GetProcessStatus() method.
        /// </summary>
        /// <param name="processOrders">The process orders.</param>
        /// <returns>
        /// A ProcessOrdersResult object. This ProcessOrdersResult object was generated by a tool.
        /// The different elements are described on the following XML Schema Definition https://plazaapi.bol.com/services/xsd/plazaapiservice-v1.xsd
        /// </returns>
        public ProcessOrdersResult ProcessOrders(ProcessOrders processOrders)
        {
            HttpWebResponse response = null;
            ProcessOrdersResult results = null;

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_url + "/services/rest/orders/v1/process/");

                Utils.HandleRequest(request, C.RequestMethods.POST, _publicKey, _privateKey);

                using (Stream reqStream = request.GetRequestStream())
                {
                    XmlSerializer s = new XmlSerializer(typeof(ProcessOrders));
                    s.Serialize(reqStream, processOrders);
                }

                SigningRequest = Utils.StringToSign.Replace("\n", Environment.NewLine);
                response = (HttpWebResponse)request.GetResponse();

                if (HttpStatusCode.OK == response.StatusCode)
                {
                    XmlSerializer ser = new XmlSerializer(typeof(ProcessOrdersResult));
                    object obj = ser.Deserialize(response.GetResponseStream());
                    results = (ProcessOrdersResult)obj;

                    var json = new JavaScriptSerializer().Serialize(obj);
                    ResponseOutput = json;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    throw ExceptionHandler.HandleResponseException((HttpWebResponse)ex.Response);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }

            return results;
        }

        /// <summary>
        /// Gets the order processing status.
        /// This code performs a simple GET on the "/services/rest/orders/v1/process/" PlazaAPI.
        /// NOTE: It is important to check for each of the items in your order if they are contained within the OrderItemsList array.
        /// If not, they have NOT been accepted by bol.com and the customer will not know that they have been shipped. Nor will you receive payment for them.
        /// All the SUCCESS statuses may make it seem like all is well but until you verify that each and every OrderItem has been accepted you cannot be sure.
        /// A sure sign of trouble is if orders that have been processed through the API have are still visible in the seller Dashboard after some time (say, a couple of hours at most)
        /// </summary>
        /// <param name="orderId">The order identifier.</param>
        /// <returns>
        /// A ProcessOrdersOverview object. This ProcessOrdersOverview object was generated by a tool.
        /// The different elements are described on the following XML Schema Definition https://plazaapi.bol.com/services/xsd/plazaapiservice-v1.xsd
        /// </returns>
        public ProcessOrdersOverview GetProcessStatus(int orderId)
        {
            HttpWebResponse response = null;
            ProcessOrdersOverview results = null;

            if (orderId < -1)
            {
                throw new Exception("Invalid (no) processing ID received.");
            }

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_url + "/services/rest/orders/v1/process/" + orderId.ToString());

                Utils.HandleRequest(request, C.RequestMethods.GET, _publicKey, _privateKey);

                SigningRequest = Utils.StringToSign.Replace("\n", Environment.NewLine);
                response = (HttpWebResponse)request.GetResponse();

                if (HttpStatusCode.OK == response.StatusCode)
                {
                    XmlSerializer ser = new XmlSerializer(typeof(ProcessOrdersOverview));
                    object obj = ser.Deserialize(response.GetResponseStream());
                    results = (ProcessOrdersOverview)obj;

                    var json = new JavaScriptSerializer().Serialize(obj);
                    ResponseOutput = json;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    throw ExceptionHandler.HandleResponseException((HttpWebResponse)ex.Response);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }

            return results;
        }

        /// <summary>
        /// Gets the payments for a specific month.
        /// NOTE: There is no status to demonstrate that a payment has been removed from this list (due to orders that got returned to the seller).
        /// </summary>
        /// <param name="year">The year.</param>
        /// <param name="month">The month.</param>
        /// <returns>
        /// A Payments object. This Payments object was generated by a tool.
        /// The different elements are described on the following XML Schema Definition https://plazaapi.bol.com/services/xsd/plazaapiservice-v1.xsd
        /// </returns>
        public Payments GetPaymentsForMonth(int year, int month)
        {
            HttpWebResponse response = null;
            Payments results = null;

            if (year < 1970 || year > 2100)
            {
                throw new Exception("Invalid Year " + year.ToString() + " Minimum value is 1970, maximum value is 2100");
            }

            if (month < 1 || month > 12)
            {
                throw new Exception("Invalid Month " + month + " Minimum value is 1, maximum value is 12");
            }

            string yearMonth = (month < 10) ? year.ToString() + "0" + month.ToString() : year.ToString() + month.ToString();

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_url + "/services/rest/payments/v1/payments/" + yearMonth);

                Utils.HandleRequest(request, C.RequestMethods.GET, _publicKey, _privateKey);

                SigningRequest = Utils.StringToSign.Replace("\n", Environment.NewLine);
                response = (HttpWebResponse)request.GetResponse();

                if (HttpStatusCode.OK == response.StatusCode)
                {
                    XmlSerializer ser = new XmlSerializer(typeof(Payments));
                    object obj = ser.Deserialize(response.GetResponseStream());
                    results = (Payments)obj;

                    var json = new JavaScriptSerializer().Serialize(obj);
                    ResponseOutput = json;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    throw ExceptionHandler.HandleResponseException((HttpWebResponse)ex.Response);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }

            return results;
        }

        public bool CreateOffer(OfferCreate offerCreate, string offerId)
        {
            HttpWebResponse response = null;
            bool succeeded = false;

            try
            {
                string requestUriString = string.Concat(_url, "/offers/v1/", offerId);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUriString);

                Utils.HandleRequest(request, C.RequestMethods.POST, _publicKey, _privateKey);

                StreamWriter writer = new StreamWriter(string.Format("E:\\plaza_api_fork\\offerCreate_{0}.xml", offerId));

                XmlSerializer test = new XmlSerializer(typeof(OfferCreate));
                test.Serialize(writer, offerCreate);
                writer.Close();

                using (Stream reqStream = request.GetRequestStream())
                {
                    XmlSerializer s = new XmlSerializer(typeof(OfferCreate));
                    s.Serialize(reqStream, offerCreate);
                }

                SigningRequest = Utils.StringToSign.Replace("\n", Environment.NewLine);
                response = (HttpWebResponse)request.GetResponse();

                succeeded = response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Accepted;// ???
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    throw ExceptionHandler.HandleResponseException((HttpWebResponse)ex.Response);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }

            return succeeded;
        }

        public string GetOffersDownloadURL(bool? published = null)
        {
            HttpWebResponse response = null;
            OfferFile offerfile = null;

            try
            {
                string filter = "";

                if (published.HasValue)
                {
                    if (published.Value)
                    {
                        filter += "?filter=PUBLISHED";
                    }
                    else
                    {
                        filter += "?filter=NOT-PUBLISHED";
                    }
                }

                string requestUriString = string.Concat(_url, "/offers/v1/export", filter);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUriString);

                Utils.HandleRequest(request, C.RequestMethods.GET, _publicKey, _privateKey);

                SigningRequest = Utils.StringToSign.Replace("\n", Environment.NewLine);
                response = (HttpWebResponse)request.GetResponse();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    XmlSerializer ser = new XmlSerializer(typeof(OfferFile));
                    object obj = ser.Deserialize(response.GetResponseStream());
                    offerfile = (OfferFile)obj;

                    var json = new JavaScriptSerializer().Serialize(obj);
                    ResponseOutput = json;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    throw ExceptionHandler.HandleResponseException((HttpWebResponse)ex.Response);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }

            return offerfile.Url;
        }

        public bool DownloadOffers(string offersUrl, string filePath)
        {
            HttpWebResponse response = null;
            StreamReader reader = null;
            bool fileSaved = false;

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(offersUrl);

                Utils.HandleRequest(request, C.RequestMethods.GET, _publicKey, _privateKey);

                SigningRequest = Utils.StringToSign.Replace("\n", Environment.NewLine);
                response = (HttpWebResponse)request.GetResponse();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream responseStream = response.GetResponseStream();
                    reader = new StreamReader(responseStream);

                    var strReponse = reader.ReadToEnd();
                    File.WriteAllText(filePath, strReponse);

                    fileSaved = true;
                }else if (response.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    // File not found
                    fileSaved = false;
                }

            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    throw ExceptionHandler.HandleResponseException((HttpWebResponse)ex.Response);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }

                if(reader != null)
                {
                    reader.Close();
                }
            }

            return fileSaved;
        }

        #endregion
    }
}
