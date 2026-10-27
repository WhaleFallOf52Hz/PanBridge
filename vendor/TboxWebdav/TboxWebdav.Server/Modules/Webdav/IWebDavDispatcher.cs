namespace TboxWebdav.Server.Modules.Webdav
{
    public interface IWebDavDispatcher
    {
        Task<WebDavResult> OptionsAsync(string path, CancellationToken cancellationToken);
        Task<WebDavResult> GetAsync(string path, CancellationToken cancellationToken);
        Task<WebDavResult> HeadAsync(string path, CancellationToken cancellationToken);
        Task<WebDavResult> PropFindAsync(string path, CancellationToken cancellationToken);
        Task<WebDavResult> PutAsync(string path, CancellationToken cancellationToken);
        Task<WebDavResult> DeleteAsync(string path, CancellationToken cancellationToken);
        Task<WebDavResult> LockAsync(string path, CancellationToken cancellationToken);
        Task<WebDavResult> UnlockAsync(string path, CancellationToken cancellationToken);
        Task<WebDavResult> MkcolAsync(string path, CancellationToken cancellationToken);
        Task<WebDavResult> PropPatchAsync(string path, CancellationToken cancellationToken);
        Task<WebDavResult> MoveAsync(string path, CancellationToken cancellationToken);
        Task<WebDavResult> CopyAsync(string path, CancellationToken cancellationToken);
    }
}
