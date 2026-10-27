using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TboxWebdav.Server.Modules.Tbox.Models
{
    public partial class TboxMoveFileDto
    {
        [JsonProperty("path")]
        public List<string> Path { get; set; }
    }
}
