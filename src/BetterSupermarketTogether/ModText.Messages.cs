using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BetterSupermarketTogether;

// Pure formatting and bounded notice serialization; Unity only supplies English.
internal static partial class ModText
{
    internal static string Get(string source) => Translate(source,English);
    internal static string Translate(string source,bool english) =>
        english && source!=null && Catalog.TryGetValue(source,out string value)?value:source;
    internal static string Format(string source,params object[] arguments) => FormatFor(English,source,arguments);
    internal static string FormatFor(bool english,string source,params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture,Translate(source,english),arguments.Select(x=>x is Message message?(object)message.Render(english):x).ToArray());

    internal sealed class Message
    {
        internal string Source { get; }
        private readonly object[] _arguments;
        internal Message(string source,params object[] arguments) { Source=source??"";_arguments=arguments??Array.Empty<object>(); }
        internal string Render(bool english) => Render(english,CultureInfo.CurrentCulture);
        private string Render(bool english,CultureInfo culture) => string.Format(culture,Translate(Source,english),
            _arguments.Select(x=>x is Message message?(object)message.Render(english,culture):x).ToArray());
        internal string WireText => Render(true,CultureInfo.InvariantCulture);
        public override string ToString() => Render(English);
        public static implicit operator Message(string source) => new Message(source);

        internal JObject ToNotice() => new JObject { ["key"]=Source,["args"]=new JArray(_arguments.Select(Argument)) };
        private static JToken Argument(object value)
        {
            if(value is Message message)return message.ToNotice();
            if(value==null)return JValue.CreateNull();
            if(value is string || value is bool || value is int || value is long || value is float || value is double || value is decimal)return JToken.FromObject(value);
            return new JValue(value.ToString()); // Freeze live arguments; never serialize delegates or runtime objects.
        }
        internal static bool TryNotice(JToken token,out Message result)
        {
            result=null;int budget=64;
            try { return Read(token,0,ref budget,out result); }
            catch(Exception ex) when(ex is FormatException || ex is InvalidCastException || ex is ArgumentException || ex is OverflowException) { return false; }
        }
        private static bool Read(JToken token,int depth,ref int budget,out Message result)
        {
            result=null;
            if(depth>8 || --budget<0 || token is not JObject obj || obj.Count!=2 || obj["key"]?.Type!=JTokenType.String ||
                !Catalog.ContainsKey((string)obj["key"]) || obj["args"] is not JArray args || args.Count>16)return false;
            var values=new object[args.Count];
            for(int i=0;i<args.Count;i++)
            {
                if(args[i] is JObject)
                {
                    if(!Read(args[i],depth+1,ref budget,out var nested))return false;values[i]=nested;
                }
                else if(args[i] is JValue value && (value.Type==JTokenType.Null || value.Type==JTokenType.String || value.Type==JTokenType.Integer || value.Type==JTokenType.Float || value.Type==JTokenType.Boolean))
                {
                    if(value.Type==JTokenType.String && ((string)value).Length>2000)return false;
                    values[i]=value.Value;
                }
                else return false;
            }
            var message=new Message((string)obj["key"],values);
            message.Render(false);message.Render(true); // Invalid format arity falls back to the saved raw text.
            result=message;return true;
        }
    }

    internal sealed class LiveArgument
    {
        private readonly Func<string> _value;
        internal LiveArgument(Func<string> value) { _value=value; }
        public override string ToString() => _value();
    }
}
