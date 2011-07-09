using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace LibCurl
{
    public class SList : IDisposable
    {
        [System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private class curl_slist
        {
            /// char*
            [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPStr)]
            public string data;

            /// curl_slist*
            public System.IntPtr next;
        }
        /// <summary>
        /// Read-only copy of the strings stored in the SList
        /// </summary>
        public List<string> Strings
        {
            get
            {
                if (_handle == IntPtr.Zero)
                    return null;

                curl_slist slist = new curl_slist();
                List<string> strings = new List<string>();

                Marshal.PtrToStructure(_handle, slist);

                while (true)
                {
                    strings.Add(slist.data);
                    if (slist.next != IntPtr.Zero)
                        Marshal.PtrToStructure(slist.next, slist);
                    else
                        break;
                }
                return strings;
            }
        }
        
        public SList(IntPtr handle)
        {
            _handle = handle;
        }
        public SList()
        {
        }

        public void Append(string data)
        {
            _handle = Curl.curl_slist_append(_handle, data);
        }

        private IntPtr _handle = IntPtr.Zero;
        public IntPtr Handle
        {
            get { return _handle; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                Curl.curl_slist_free_all(_handle);
                _handle = IntPtr.Zero;
            }
        }

        #endregion
    }
}
