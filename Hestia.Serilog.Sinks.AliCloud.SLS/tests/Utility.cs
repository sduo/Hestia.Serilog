using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog.Events;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Hestia.Serilog.Sinks.AliCloud.SLS.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public sealed partial class Utility
    {
        private static readonly byte[] key = Encoding.UTF8.GetBytes("Hestia.Serilog");
        private static readonly byte[] content = Encoding.UTF8.GetBytes("Hestia.Serilog");

        [TestMethod]
        public void Md5()
        {
            Assert.AreEqual(SLS.Utility.Md5(content), Original.CalculateContentMd5(content));            
        }

        [TestMethod]
        public void HMACSHA1()
        {
            Assert.AreEqual(SLS.Utility.HMAC_SHA1(key, content), Original.ComputeSignature(key, content));
        }

        [TestMethod]
        public void EncodeURI()
        {  
            var content = "+*~/";
            var encoding = Encoding.UTF8;
            Assert.AreEqual(SLS.Utility.EncodeURI(content, encoding), Original.encodeUrl(content, encoding));
        }

        [TestMethod]
        public void ParseQueryString()
        {
            var querystring = "key=shard";
            var query = SLS.Utility.ParseQueryString(querystring);
            Assert.HasCount(1, query);
            Assert.AreEqual("shard", query["key"]);
        }

        [TestMethod]
        public void Version()
        {            
            Assert.IsNotNull(SLS.Utility.Version);
        }
    }
}
