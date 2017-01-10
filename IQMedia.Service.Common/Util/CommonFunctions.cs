using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

using System.IO;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections;
using System.Data.SqlClient;
using System.Data;

namespace IQMedia.Service.Common.Util
{
    public static class CommonFunctions
    {
        private static string AesKeyLicense = "6D372F5167584155694672674D486B67";
        private static string AesIVLicense = "516341644D4A3373";

        public static string DecryptStringFromBytes_Aes(string encrypteString)// byte[] cipherText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (string.IsNullOrWhiteSpace(encrypteString))
                throw new ArgumentNullException("encrypted string is null");

            //byte[] cipherText = Convert.FromBase64String(encrypteString.Replace(" ", "+"));
            byte[] cipherText = Convert.FromBase64String(encrypteString);

            //byte[] cipherText = StringToUTF8ByteArray(encrypteString);            
            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            // Create an AesManaged object
            // with the specified key and IV.
            using (AesManaged aesAlg = new AesManaged())
            {
                UTF8Encoding encoding = new UTF8Encoding();
                byte[] Key = encoding.GetBytes("43DD9199E882F08814E1864B45E4F117");
                byte[] IV = encoding.GetBytes("40275DC0B57CD8D6");


                aesAlg.Key = Key;// aesAlg.Key;
                aesAlg.IV = IV;// aesAlg.IV;

                // Create a decrytor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {

                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }

            }

            return plaintext;

        }

        public enum CategoryType
        {
            [Description("Social Media")]
            SocialMedia,
            [Description("Online News")]
            NM,
            [Description("Print Media")]
            PM,
            [Description("TV")]
            TV,
            [Description("Twitter")]
            TW,
            [Description("Forum")]
            Forum,
            [Description("Blog")]
            Blog
        }

        public static object DeserialiazeXml(string p_XMLString, object p_Deserialization)
        {
            StringReader _StringReader;
            XmlTextReader _XmlTextReader;
            
            XmlSerializer _XmlSerializer = new XmlSerializer(p_Deserialization.GetType());

            _StringReader = new StringReader(p_XMLString);
            _XmlTextReader = new XmlTextReader(_StringReader);

            p_Deserialization = (object)_XmlSerializer.Deserialize(_XmlTextReader);

            _StringReader.Close();
            _XmlTextReader.Close();

            return p_Deserialization;
        }

        public static string GetEnumDescription(Enum value)
        {
            FieldInfo fi = value.GetType().GetField(value.ToString());

            DescriptionAttribute[] attributes =
                (DescriptionAttribute[])fi.GetCustomAttributes(
                typeof(DescriptionAttribute),
                false);

            if (attributes != null &&
                attributes.Length > 0)
                return attributes[0].Description;
            else
                return value.ToString();
        }

        public static string EncryptLicenseStringAES(string rawString)
        {
            byte[] encrypted;


            UTF8Encoding encoding = new UTF8Encoding();

            // Create an AesManaged object
            // with the specified key and IV.
            using (AesManaged aesManaged = new AesManaged())
            {
                aesManaged.Key = encoding.GetBytes(AesKeyLicense);
                aesManaged.IV = encoding.GetBytes(AesIVLicense);

                // Create a decrytor to perform the stream transform.
                ICryptoTransform encryptor = aesManaged.CreateEncryptor(aesManaged.Key, aesManaged.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {

                            //Write all data to the stream.
                            swEncrypt.Write(rawString);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }
            // Return the encrypted bytes from the memory stream.
            return Convert.ToBase64String(encrypted);
        }

        public static string GetWordsAround(string InputString, string Keyword, int BeforeWords, int AfterWords, string Seprator, string WordPrefexSpecialChar = "<")
        {
            string returnStr = string.Empty;
            InputString = Regex.Replace(InputString, "\\s+", " ");
            //string regex = "(?:[a-zA-Z'-<>]+[^a-zA-Z'-]+){0," + BeforeWords + "}\\b" + Keyword + "\\b(?:[^a-zA-Z'-]+[a-zA-Z'-<>]+){0," + AfterWords + "}";
            //string regex = "((?:[\\s<>]\\S*){0," + BeforeWords + "})" + WordPrefexSpecialChar + "\\b" + Keyword + "\\b((?:\\S*\\s+){0," + AfterWords + "})";
            string regex = "((?:[\\s<>]\\S*){0," + BeforeWords + "})" + WordPrefexSpecialChar + "\\b" + Keyword + "\\b((?:(<(([^>]*)?)>[^<].*?(</span>)|\\S*\\s+)){0," + AfterWords + "})";
            MatchCollection collection = Regex.Matches(InputString, regex);
            int i = 1;
            foreach (Match m in collection)
            {
                if (i == collection.Count)
                {
                    returnStr = returnStr + m.Value;
                }
                else
                {
                    returnStr = returnStr + m.Value + Seprator;
                }
                i = i + 1;
            }
            return returnStr;
        }

        public static string GenerateRandomString(int length = 8)
        {
            Random random = new Random();

            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            string strRandom = new string(
                Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
            return strRandom;
        }

        //delegate DateTime ConvertDate(DateTime gmtDateTime, double gmtHours, double dstHours);

        public static void ConvertGMTDateToLocalDate(dynamic p_Object,double p_GMTHours,double p_DSTHours,string p_PropName)
        {
            Func<DateTime,double,double,DateTime> ConvertDate = (gmtDateTime, gmtHours, dstHours) =>
            {
                if (gmtDateTime.IsDaylightSavingTime())
                {

                    gmtDateTime = gmtDateTime.AddHours(gmtHours + dstHours);
                }
                else
                {
                    gmtDateTime = gmtDateTime.AddHours(gmtHours);
                }

                return gmtDateTime;
            };

            if (p_Object.GetType().IsGenericType && p_Object is IEnumerable)
            {
                foreach (var obj in p_Object)
                {
                    var gmtDateTime = (DateTime)obj.GetType().GetProperty(p_PropName).GetValue(obj, null);

                    obj.GetType().GetProperty(p_PropName).SetValue(obj, ConvertDate(gmtDateTime, p_GMTHours, p_DSTHours), null);
                }
            }
            else
            {
                p_Object.GetType().GetProperty(p_PropName).SetValue(p_Object, ConvertDate(p_Object.GetType().GetProperty(p_PropName).GetValue(p_Object, null), p_GMTHours, p_DSTHours), null);
            }
        }

        public static void WriteException(string connStr, string serviceName, Exception ex, long? taskID = null)
        {
            string taskInfo = String.Empty;
            if (taskID.HasValue)
            {
                taskInfo = "Task " + taskID + ": ";
            }

            using (var conn = new SqlConnection(connStr))
            {
                conn.Open();

                using (var cmd = conn.GetCommand("usp_IQMediaGroupExceptions_Insert", CommandType.StoredProcedure))
                {
                    cmd.Parameters.AddWithValue("@ExceptionStackTrace", "Inner Exception : " + ex.InnerException + " Stack Trace : " + ex.StackTrace);
                    cmd.Parameters.AddWithValue("@ExceptionMessage", taskInfo + ex.Message);
                    cmd.Parameters.AddWithValue("@CreatedBy", serviceName);
                    cmd.Parameters.AddWithValue("@CreatedDate", DateTime.Now);
                    cmd.Parameters.AddWithValue("@CustomerGuid", DBNull.Value);
                    cmd.Parameters.Add(new SqlParameter("@IQMediaGroupExceptionKey", 0) { Direction = ParameterDirection.Output });

                    cmd.ExecuteNonQuery();
                }
            }
        }
    }

    public class Article
    {
        public string ArticleID { get; set; }
        public string SearchTerm { get; set; }

    }


}
