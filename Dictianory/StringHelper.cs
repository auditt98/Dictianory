
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dictianory
{
    public class StringHelper
    {
        public string SQLChecker(string str) { return str.Replace("'", "''"); }

        public string getCorrectionPanelDefaultString() { return "Gõ hoặc dán văn bản tiếng Anh vào đây và ấn Kiểm Tra."; }

        public string errorWordNotFound() { return "Không thấy từ này."; }

        public string getRegexSearchResultWordType() { return @"(?<=\*  ).+(?=\n)"; }

        public string getRegexSearchResultWordDef() { return @"(?<=\- ).+(?=\n)"; }

        public string getRegexSearchResultWordExample() { return @"(?<=\t).+(?=\n)"; }

        public string getSqlTimeFormat() { return "yyyy-MM-dd HH:mm:ss"; }

        public string getDataTrainingFileName() { return "DataTraining.txt"; }

        public string getRegexWordPatern() { return @"\b(\w*[a-zA-Z'-]\w*)\b"; }

        public string infoPleaseWait() { return "Chờ chút cho bộ sửa lỗi khởi động ^^"; }
    }
}
