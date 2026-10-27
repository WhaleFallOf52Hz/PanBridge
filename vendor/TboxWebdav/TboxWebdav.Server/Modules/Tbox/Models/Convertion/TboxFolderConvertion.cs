using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TboxWebdav.Server.Modules.Tbox.Models.Convertion
{
    public static class TboxFolderConvertion
    {
        public static TboxFolderInfoDto ToTboxFolderInfoDto(this TboxMergedItemDto dto)
        {
            return new TboxFolderInfoDto
            {
                AuthorityList = dto.AuthorityList,
                CreationTime = dto.CreationTime,
                ModificationTime = dto.ModificationTime,
                ETag = dto.ETag,
                //IsAuthorized = dto.IsAuthorized,
                Name = dto.Name,
                Path = dto.Path,
                SensitiveWordAuditStatus = dto.SensitiveWordAuditStatus,
                Type = dto.Type,
                UserId = dto.UserId,
                //UserOrgId = dto.UserOrgId,
                VirusAuditStatus = dto.VirusAuditStatus,
            };
        }
    }
}
