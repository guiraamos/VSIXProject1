

namespace Analyzer
{
    public static class Util
    {
        private static readonly string[] TextoPadraoremover = { "Controller" };

        public static string TrataNome(string pretendingClassName)
        {
            string ret = pretendingClassName;
            foreach (var stringRemove in TextoPadraoremover)
            {
                ret = pretendingClassName.Replace(stringRemove, "");
            }

            return ret;
        }
    }
}
