using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace LibCurl
{
    public class Curl : IDisposable
    {
        private const String m_libCurlBase = "libcurl";

        #region Object pinning
        /// <summary>
        /// Free the pinned object
        /// </summary>
        /// <param name="handle"></param>
        void FreeHandle(ref IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return;
            GCHandle handleCallback = GCHandle.FromIntPtr(handle);
            handleCallback.Free();
            handle = IntPtr.Zero;
        }
        /// <summary>
        /// Pin the object in memory so the C function can find it
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        IntPtr GetHandle(object obj)
        {
            if (obj == null)
                return IntPtr.Zero;
            return GCHandle.ToIntPtr(GCHandle.Alloc(obj, GCHandleType.Pinned));
        }
        /// <summary>
        /// Returns the object passed to a Set...Data function.
        /// Cast back to the original object.
        /// </summary>
        /// <param name="userdata"></param>
        /// <returns></returns>
        public static object GetObject(IntPtr userdata)
        {
            if (userdata == IntPtr.Zero)
                return null;
            GCHandle handle = GCHandle.FromIntPtr(userdata);
            return handle.Target;
        }
        #endregion


        #region Write callback CURLOPT_WRITEFUNCTION
        /// <summary>
        /// Invoked when data is recevied.  
        /// Return the number of bytes actually taken care of.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="size"></param>
        /// <param name="userdata"></param>
        /// <returns></returns>
        public delegate int GenericCallbackDelegate(byte[] buffer, int size, object userdata);

        private delegate int _GenericCallbackDelegate(IntPtr ptr, int sz, int nmemb, IntPtr userdata);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.StdCall)]
        private static extern CURLcode curl_easy_setopt(IntPtr pCurl, CURLoption opt, _GenericCallbackDelegate callback);
        private IntPtr _OnWriteCallbackHandle = IntPtr.Zero;
        private GenericCallbackDelegate _OnWriteCallback;
        /// <summary>
        /// Invoked when data is recevied that needs to be saved.  
        /// The data buffer will not be null terminated.
        /// Return the number of bytes actually taken care of.
        /// If that amount differs from the amount passed to your function, it'll signal an error to the library. This will abort the transfer and return CURLE_WRITE_ERROR.
        /// </summary>
        public GenericCallbackDelegate OnWriteCallback
        {
            set
            {
                if (_OnWriteCallbackHandle != IntPtr.Zero)
                {
                    FreeHandle(ref _OnWriteCallbackHandle);
                    _OnWriteCallback = null;
                }
                if (value != null)
                {
                    _OnWriteCallback = value;
                    _GenericCallbackDelegate cb = new _GenericCallbackDelegate(internal_OnWriteCallback);
                    _OnWriteCallbackHandle = GetHandle(cb);
                    curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_WRITEFUNCTION, cb);
                }
            }
        }
        int internal_OnWriteCallback(IntPtr ptrBuffer, int sz, int nmemb, IntPtr ptrUserdata)
        {
            if (_OnWriteCallback != null)
            {
                int bytes = sz * nmemb;
                byte[] b = new byte[bytes];
                Marshal.Copy(ptrBuffer, b, 0, bytes);

                object userdata = GetObject(ptrUserdata);
                return _OnWriteCallback(b, bytes, userdata);
            }
            return 0;
        }

        private IntPtr _WriteData = IntPtr.Zero;
        /// <summary>
        /// Object to pass to OnWriteCallback.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public CURLcode SetWriteData(object data)
        {
            _WriteData = GetHandle(data);
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_WRITEDATA, _WriteData);
        }
        #endregion

        #region Read callback CURLOPT_READFUNCTION
        /// <summary>
        /// Fill the buffer with data to send to the remote server.
        /// The buffer may be filled with at most size bytes.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="sz"></param>
        /// <param name="nmemb"></param>
        /// <param name="userdata"></param>
        /// <returns></returns>
        public delegate int ReadCallbackDelegate(out byte[] buffer, int size, object userdata);

        private IntPtr _OnReadCallbackHandle = IntPtr.Zero;
        private ReadCallbackDelegate _OnReadCallback;
        /// <summary>
        /// This function gets called by libcurl as soon as it needs to read data in order to send it to the peer.
        /// The data buffer may be filled with at most size bytes. 
        /// Your function must return the actual number of bytes that you stored in that memory area.
        /// Returning 0 will signal end-of-file to the library and cause it to stop the current transfer.
        /// </summary>
        public ReadCallbackDelegate OnReadCallback
        {
            set
            {
                if (_OnReadCallbackHandle != IntPtr.Zero)
                {
                    FreeHandle(ref _OnReadCallbackHandle);
                    _OnReadCallback = null;
                }
                if (value != null)
                {
                    _OnReadCallback = value;
                    var cb = new _GenericCallbackDelegate(internal_OnReadCallback);
                    _OnReadCallbackHandle = GetHandle(cb); 
                    curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_READFUNCTION, cb);
                }
            }
        }
        int internal_OnReadCallback(IntPtr ptrBuffer, int sz, int nmemb, IntPtr ptrUserdata)
        {
            if (_OnReadCallback != null)
            {
                object userdata = GetObject(ptrUserdata);
                byte[] buffer;
                int size = _OnReadCallback(out buffer, sz * nmemb, userdata);
                if (size == 0 || buffer == null)
                    return 0;
                Marshal.Copy(buffer, 0, ptrBuffer, size);
                return buffer.Length;
            }
            return 0;
        }

        private IntPtr _ReadData = IntPtr.Zero;
        /// <summary>
        /// Object to pass to OnReadCallback.
        /// Use <see cref="curl.GetObject"/> to convert the passed IntPtr back into the object, then cast.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public CURLcode SetReadData(object data)
        {
            _ReadData = GetHandle(data);
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_READDATA, _ReadData);
        }
        #endregion

        #region Progress callback CURLOPT_PROGRESSFUNCTION
        /// <summary>
        /// This function gets called by libcurl instead of its internal equivalent with a frequent interval during operation 
        /// (roughly once per second or sooner) no matter if data is being transfered or not. 
        /// Unknown/unused argument values passed to the callback will be set to zero 
        /// (like if you only download data, the upload size will remain 0). 
        /// Returning a non-zero value from this callback will cause libcurl to abort the transfer and return CURLE_ABORTED_BY_CALLBACK.
        ///
        /// CURLOPT_NOPROGRESS is automatically set to 0.
        /// </summary>
        /// <param name="userdata"></param>
        /// <param name="dlTotal"></param>
        /// <param name="dlNow"></param>
        /// <param name="ulTotal"></param>
        /// <param name="ulNow"></param>
        /// <returns></returns>
        public delegate int ProgressCallbackDelegate(object userdata, double dlTotal, double dlNow, double ulTotal, double ulNow);

        private delegate int _ProgressCallbackDelegate(IntPtr extraData, double dlTotal, double dlNow, double ulTotal, double ulNow);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.StdCall)]
        private static extern CURLcode curl_easy_setopt(IntPtr pCurl, CURLoption opt, _ProgressCallbackDelegate callback);
        private IntPtr _OnProgressCalbackHandle = IntPtr.Zero;
        private ProgressCallbackDelegate _OnProgressCallback;
        /// <summary>
        /// This function gets called by libcurl instead of its internal equivalent with a frequent interval during operation 
        /// (roughly once per second or sooner) no matter if data is being transfered or not. 
        /// Unknown/unused argument values passed to the callback will be set to zero 
        /// (like if you only download data, the upload size will remain 0). 
        /// Returning a non-zero value from this callback will cause libcurl to abort the transfer and return CURLE_ABORTED_BY_CALLBACK.
        /// 
        /// CURLOPT_NOPROGRESS is automatically set to 0.
        /// </summary>
        public ProgressCallbackDelegate OnProgressCalback
        {
            set
            {
                if (_OnProgressCalbackHandle != IntPtr.Zero)
                {
                    FreeHandle(ref _OnProgressCalbackHandle);
                    _OnProgressCallback = null;
                }
                if (value != null)
                {
                    _OnProgressCallback = value;
                    var cb = new _ProgressCallbackDelegate(internal_ProgressCallback);
                    curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_PROGRESSFUNCTION, cb);
                    _OnProgressCalbackHandle = GetHandle(cb);
                    curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_NOPROGRESS, 0L);
                }
            }
        }
        int internal_ProgressCallback(IntPtr ptrUserdata, double dlTotal, double dlNow, double ulTotal, double ulNow)
        {
            if (_OnProgressCallback != null)
            {
                object userdata = GetObject(ptrUserdata);
                return _OnProgressCallback(userdata, dlTotal, dlNow, ulTotal, ulNow);
            }
            return 0;
        }

        private IntPtr _ProgressData = IntPtr.Zero;
        /// <summary>
        /// Object to pass to OnProgressCallback.
        /// Use <see cref="curl.GetObject"/> to convert the passed IntPtr back into the object, then cast.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public CURLcode SetProgressData(object data)
        {
            _ProgressData = GetHandle(data);
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_PROGRESSDATA, _ProgressData);
        }
        #endregion

        #region Header callback CURLOPT_HEADERFUNCTION
        IntPtr _OnHeaderCallbackHandle = IntPtr.Zero;
        GenericCallbackDelegate _OnHeaderCallback;
        /// <summary>
        /// This function gets called by libcurl as soon as it has received header data. 
        /// The header callback will be called once for each header and only complete header lines are passed on to the callback.
        /// Do not assume that the header line is zero terminated!
        /// The callback function must return the number of bytes actually taken care of. 
        /// If that amount differs from the amount passed to your function, it'll signal an error to the library. 
        /// This will abort the transfer and return CURL_WRITE_ERROR.
        /// </summary>
        public GenericCallbackDelegate OnHeaderCallback
        {
            set
            {
                if (_OnHeaderCallbackHandle != IntPtr.Zero)
                {
                    FreeHandle(ref _OnHeaderCallbackHandle);
                    _OnHeaderCallback = null;
                }
                if (value != null)
                {
                    _OnHeaderCallback = value;
                    var cb = new _GenericCallbackDelegate(internal_OnHeaderCallback);
                    _OnHeaderCallbackHandle = GetHandle(cb);
                    curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_HEADERFUNCTION, cb);
                }
            }
        }
        int internal_OnHeaderCallback(IntPtr ptrBuffer, int sz, int nmemb, IntPtr ptrUserdata)
        {
            if (_OnHeaderCallback != null)
            {
                int bytes = sz * nmemb;
                byte[] b = new byte[bytes];
                Marshal.Copy(ptrBuffer, b, 0, bytes);

                object userdata = GetObject(ptrUserdata);

                return _OnHeaderCallback(b, bytes, userdata);
            }
            return 0;
        }
        private IntPtr _HeaderData = IntPtr.Zero;
        /// <summary>
        /// Object to pass to OnHeaderCallback.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public CURLcode SetHeaderData(object data)
        {
            _HeaderData = GetHandle(data);
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_HEADERDATA, _HeaderData);
        }
        #endregion

        #region Debug callback CURLOPT_DEBUGFUNCTION
        /// <summary>
        /// This replaces the standard debug function used when CURLOPT_VERBOSE is in effect.
        /// This callback receives debug information, as specified with the curl_infotype argument. 
        /// </summary>
        /// <param name="infoType"></param>
        /// <param name="message"></param>
        /// <param name="userdata"></param>
        public delegate void DebugCallbackDelegate(CURLINFOTYPE infoType, string message, object userdata);

        private delegate int _DebugCallbackDelegate(IntPtr ptrCurl, CURLINFOTYPE infoType, string message, int size, IntPtr ptrUserData);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.StdCall)]
        private static extern CURLcode curl_easy_setopt(IntPtr pCurl, CURLoption opt, _DebugCallbackDelegate callback);

        IntPtr _OnDebugCallbackHandle = IntPtr.Zero;
        DebugCallbackDelegate _OnDebugCallback;
        /// <summary>
        /// This replaces the standard debug function used when CURLOPT_VERBOSE is in effect.
        /// This callback receives debug information, as specified with the curl_infotype argument. 
        /// </summary>
        public DebugCallbackDelegate OnDebugCallback
        {
            set
            {
                if (_OnDebugCallbackHandle != IntPtr.Zero)
                {
                    FreeHandle(ref _OnDebugCallbackHandle);
                    _OnDebugCallback = null;
                }
                if (value != null)
                {
                    _OnDebugCallback = value;
                    var cb = new _DebugCallbackDelegate(internal_OnDebugCallback);
                    _OnDebugCallbackHandle = GetHandle(cb);
                    curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_DEBUGFUNCTION, cb);
                }
            }
        }
        int internal_OnDebugCallback(IntPtr ptrCurl, CURLINFOTYPE infoType, string message, int size, IntPtr ptrUserdata)
        {
            if (_OnDebugCallback != null)
            {
                object userdata = GetObject(ptrUserdata);

                _OnDebugCallback(infoType, message, userdata);
            }
            return 0;
        }
        private IntPtr _DebugData = IntPtr.Zero;
        /// <summary>
        /// Object to pass to OnDebugCallback.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public CURLcode SetDebugData(object data)
        {
            _DebugData = GetHandle(data);
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_DEBUGDATA, _DebugData);
        }
        #endregion

        /// <summary>
        /// Set the post fields
        /// </summary>
        /// <param name="postFields"></param>
        /// <returns></returns>
        public CURLcode SetPostFields(string postFields)
        {
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_POSTFIELDS, postFields);
        }
        /// <summary>
        /// Set the Url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public CURLcode SetUrl(string url)
        {
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_URL, url);
        }
        public CURLcode SetPostFieldSize(int size)
        {
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_POSTFIELDSIZE, size);
        }

        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern IntPtr curl_slist_append(IntPtr slist, string data);

        #region Global init/cleanup
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLcode curl_global_init(int flags);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void curl_global_cleanup();
        #endregion
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern IntPtr curl_escape(String url, int length);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern IntPtr curl_unescape(String url, int length);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void curl_free(IntPtr p);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr curl_version();

        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr curl_easy_init();
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void curl_easy_cleanup(IntPtr pCurl);

        #region curl_easy_setopt
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLcode curl_easy_setopt(IntPtr pCurl, CURLoption opt, IntPtr parm);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLcode curl_easy_setopt(IntPtr pCurl, CURLoption opt, string parm);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLcode curl_easy_setopt(IntPtr pCurl, CURLoption opt, long parm);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLcode curl_easy_setopt(IntPtr pCurl, CURLoption opt, bool parm);
        #endregion

        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLcode curl_easy_perform(IntPtr pCurl);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr curl_easy_duphandle(IntPtr pCurl);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr curl_easy_strerror(CURLcode err);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLcode curl_easy_getinfo(IntPtr pCurl, CURLINFO info, out IntPtr pInfo);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl, EntryPoint = "curl_easy_getinfo")]
        internal static extern CURLcode curl_easy_getinfo_64(IntPtr pCurl, CURLINFO info, ref double dblVal);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void curl_easy_reset(IntPtr pCurl);

        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr curl_multi_init();
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLMcode curl_multi_cleanup(IntPtr pmulti);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLMcode curl_multi_add_handle(IntPtr pmulti, IntPtr peasy);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLMcode curl_multi_remove_handle(IntPtr pmulti, IntPtr peasy);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr curl_multi_strerror(CURLMcode errorNum);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLMcode curl_multi_perform(IntPtr pmulti, ref int runningHandles);

        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void curl_formfree(IntPtr pForm);

        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr curl_share_init();
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLSHcode curl_share_cleanup(IntPtr pShare);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr curl_share_strerror(CURLSHcode errorCode);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLSHcode curl_share_setopt(IntPtr pShare, CURLSHoption optCode, IntPtr option);
        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern CURLSHcode curl_slist_free_all(IntPtr pList);

        [DllImport(m_libCurlBase, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr curl_version_info(CURLversion ver);

        public Curl()
        {
            m_pCURL = curl_easy_init();
            SetWriteData(null);
            SetReadData(null);
            SetProgressData(null);
        }

        private static CURLcode sm_curlCode;
        private IntPtr m_pCURL;

        static Curl()
        //public static CURLcode GlobalInit(int flags)
        {
            sm_curlCode = curl_global_init((int)CURLinitFlag.CURL_GLOBAL_ALL);
        }
        /// <summary>
        /// Version of CUrl
        /// </summary>
        /// <returns></returns>
        public static string Version()
        {
            return Marshal.PtrToStringAnsi(curl_version());
        }
        /// <summary>
        /// Set the Curl option
        /// </summary>
        /// <param name="option"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public CURLcode SetOpt(CURLoption option, string parameter)
        {
            return curl_easy_setopt(m_pCURL, option, parameter);
        }
        /// <summary>
        /// Set the Curl option
        /// </summary>
        /// <param name="option"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public CURLcode SetOpt(CURLoption option, long param)
        {
            return curl_easy_setopt(m_pCURL, option, param);
        }
        /// <summary>
        /// Set the Curl option
        /// </summary>
        /// <param name="option"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public CURLcode SetOpt(CURLoption option, IntPtr param)
        {
            return curl_easy_setopt(m_pCURL, option, param);
        }
        /// <summary>
        /// Perform the curl operation
        /// </summary>
        /// <returns></returns>
        public CURLcode Perform()
        {
            return curl_easy_perform(m_pCURL);
        }
        /// <summary>
        /// Cleanup all resources
        /// </summary>
        public void Cleanup()
        {
            curl_easy_cleanup(m_pCURL);
            m_pCURL = IntPtr.Zero;
            OnWriteCallback = null;
            OnReadCallback = null;
            OnProgressCalback = null;
            FreeHandle(ref _WriteData);
            FreeHandle(ref _ReadData);
            FreeHandle(ref _ProgressData);
        }
        /// <summary>
        /// Set the user agent
        /// </summary>
        /// <param name="agent"></param>
        /// <returns></returns>
        public CURLcode SetUserAgent(string agent)
        {
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_USERAGENT, agent);
        }
        /// <summary>
        /// Follow
        /// </summary>
        /// <param name="flag"></param>
        /// <returns></returns>
        public CURLcode SetFollowLocation(bool flag)
        {
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_FOLLOWLOCATION, flag);
        }
        /// <summary>
        /// Post
        /// </summary>
        /// <returns></returns>
        public CURLcode SetPost()
        {
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_POST, true);
        }
        /// <summary>
        /// Upload
        /// </summary>
        /// <returns></returns>
        public CURLcode SetUpload()
        {
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_UPLOAD, true);
        }
        /// <summary>
        /// Custom "POST" request
        /// </summary>
        /// <returns></returns>
        public CURLcode SetCustomPostRequest()
        {
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_CUSTOMREQUEST, "POST");
        }
        /// <summary>
        /// Set the size of the upload
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public CURLcode SetUploadSize(int value)
        {
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_INFILESIZE, value);
        }
        /// <summary>
        /// Put
        /// </summary>
        /// <returns></returns>
        public CURLcode SetPut()
        {
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_PUT, true);
        }
        /// <summary>
        /// Set the Headers
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public CURLcode SetHeader(SList list)
        {
            return curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_HTTPHEADER, list.Handle);
        }
        /// <summary>
        /// Get the current cookies.
        /// You must have already have called EnableCookies()
        /// </summary>
        /// <returns></returns>
        public SList GetCookies()
        {
            IntPtr pCookies;
            curl_easy_getinfo(m_pCURL, CURLINFO.CURLINFO_COOKIELIST, out pCookies);
            SList list = new SList(pCookies);
            return list;
        }
        /// <summary>
        /// Enable cookies and read from the file specified.
        /// If the file is not present, it is not created.  
        /// Use SaveCookies to save to the file.
        /// </summary>
        /// <param name="file"></param>
        public void EnableCookies(string file)
        {
            curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_COOKIEFILE, file);
        }
        /// <summary>
        /// Save the cookies to the file specified.
        /// </summary>
        /// <param name="file"></param>
        public void SaveCookies(string file)
        {
            curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_COOKIEJAR, file);
        }
        /// <summary>
        /// Response timeout
        /// </summary>
        /// <param name="value"></param>
        public void SetTimeout(int value)
        {
            curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_FTP_RESPONSE_TIMEOUT, value);
        }
        /// <summary>
        /// Get the curl error
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public string GetError(CURLcode code)
        {
            IntPtr ptr = curl_easy_strerror(code);
            return Marshal.PtrToStringAnsi(ptr);
        }
        /// <summary>
        /// Enable verbose messages
        /// </summary>
        public void Verbose()
        {
            curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_VERBOSE, true);
        }
        /// <summary>
        /// Disable verify SSL
        /// </summary>
        public void DisableVerifySSL()
        {
            curl_easy_setopt(m_pCURL, CURLoption.CURLOPT_SSL_VERIFYPEER, 0L);
        }

        #region IDisposable Members

        public void Dispose()
        {
            Cleanup();
        }

        #endregion
    }
}