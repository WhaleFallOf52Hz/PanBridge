using System;

namespace NutzCode.Libraries.Web
{
    public class SeekableWebParameters : WebParameters
    {
        //public long InitialLength { get; set; } 
        //public string Key { get; private set; }
        public SeekableWebParameters(Uri url) : base(url) //, string uniquekey, long initialLength
        {
            //InitialLength = initialLength;
            //Key = uniquekey;
        }
        //public override WebParameters Clone()
        //{
        //    SeekableWebParameters n = new SeekableWebParameters(Url, Key, InitialLength);
        //    this.CopyTo(n);
        //    return n;
        //}
    }
}
