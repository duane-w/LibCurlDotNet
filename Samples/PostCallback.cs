using System;
using LibCurl;

namespace Samples
{
    class PostCallback
    {
        const string data = "url=index%3Dstripbooks&field-keywords=Topology&Go.x=10&Go.y=10";
        internal class WriteThis
        {
            internal int position;
            internal int sizeleft;
        }
        public void Perform()
        {
            try
            {
                using (Curl curl = new Curl())
                {
                    WriteThis amazon = new WriteThis();
                    amazon.position = 0;
                    amazon.sizeleft = data.Length;

                    //Curl.GlobalInit((int)CURLinitFlag.CURL_GLOBAL_ALL);

                    curl.OnWriteCallback = new Curl.GenericCallbackDelegate(OnWriteData);

                    curl.OnReadCallback = new Curl.ReadCallbackDelegate(OnReadData);
                    curl.SetReadData(amazon);
                    curl.SetPostFieldSize(amazon.sizeleft);

                    curl.OnHeaderCallback = new Curl.GenericCallbackDelegate(OnHeaderData);

                    curl.SetUserAgent("Mozilla 4.0 (compatible; MSIE 6.0; Win32");
                    curl.SetUrl("http://www.amazon.com/exec/obidos/search-handle-form/002-5928901-6229641");
                    curl.SetPost();
                    curl.SetFollowLocation(true);

                    SList slist = new SList();
                    slist.Append("Accept: moo");
                    slist.Append("User-Agent: my agent");
                    curl.SetHeader(slist);

                    curl.EnableCookies("");

                    curl.Perform();

                    using (SList cookies = curl.GetCookies())
                    {
                        foreach (string cookie in cookies.Strings)
                            Console.WriteLine("{0}", cookie);
                    }

                    slist.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        int OnHeaderData(byte[] buf, int size, object userdata)
        {
            //Console.Write(System.Text.Encoding.UTF8.GetString(buf));
            return size;
        }
        int OnReadData(out byte[] buf, int size, object userdata)
        {
            buf = null;
            WriteThis wt = (WriteThis)userdata;
            if (size < 1)
                return 0;

            if (wt.sizeleft > 0)
            {
                buf = new byte[1];
                buf[0] = (byte)data[wt.position];
                wt.position++;
                wt.sizeleft--;
                return 1;
            }
            return 0;
        }

        int OnWriteData(byte[] buf, int size, object userdata)
        {
            Console.Write(System.Text.Encoding.UTF8.GetString(buf));
            return size;
        }
    }
}
