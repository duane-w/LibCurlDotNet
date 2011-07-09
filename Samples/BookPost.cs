using System;
using LibCurl;

namespace Samples
{
    class BookPost
    {
        public void Perform()
        {
            try
            {
                using (Curl curl = new Curl())
                {
                    //Curl.GlobalInit((int)CURLinitFlag.CURL_GLOBAL_ALL);

                    curl.OnWriteCallback = new Curl.GenericCallbackDelegate(OnWriteData);
                    curl.SetWriteData(null);

                    // simple post - with a string
                    curl.SetPost();
                    curl.SetPostFields("url=index%3Dstripbooks&field-keywords=Topology&Go.x=10&Go.y=10");

                    curl.SetUserAgent("Mozilla 4.0 (compatible; MSIE 6.0; Win32");
                    curl.SetFollowLocation(true);
                    curl.SetUrl("http://www.amazon.com/exec/obidos/search-handle-form/002-5928901-6229641");

                    curl.Perform();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        int OnWriteData(byte[] buf, int size, object userdata)
        {
            Console.Write(System.Text.Encoding.UTF8.GetString(buf));
            return size;
        }
    }
}
