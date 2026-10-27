using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TboxWebdav.Server.Modules.Tbox.Models.Convertion
{
    public static class TboxFileConvertion
    {
        public static TboxFileInfoDto ToTboxFileInfoDto(this TboxMergedItemDto dto)
        {
            return new TboxFileInfoDto
            {
                AuthorityList = dto.AuthorityList,
                ContentType = dto.ContentType,
                Crc64 = dto.Crc64,
                CreationTime = dto.CreationTime,
                ETag = dto.ETag,
                FileType = dto.FileType,
                //HistorySize = dto.HistorySize,
                //IsAuthorized = dto.IsAuthorized,
                ModificationTime = dto.ModificationTime,
                Name = dto.Name,
                Path = dto.Path,
                PreviewAsIcon = dto.PreviewAsIcon,
                PreviewByCi = dto.PreviewByCi,
                PreviewByDoc = dto.PreviewByDoc,
                SensitiveWordAuditStatus = dto.SensitiveWordAuditStatus,
                Size = dto.Size,
                Type = dto.Type,
                VirusAuditStatus = dto.VirusAuditStatus,
                //TagList = dto.TagList,
                UserId = dto.UserId,
                //UserOrgId = dto.UserOrgId,
            };
        }
    }
}
