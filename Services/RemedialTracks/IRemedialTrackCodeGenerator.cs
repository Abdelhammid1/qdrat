namespace QdratNew.Services.RemedialTracks
{
    public interface IRemedialTrackCodeGenerator
    {
        /// <summary>RTK-{yyyy}-{0000} حسب سنة UTC، تسلسل لكل سنة.</summary>
        Task<string> NextTrackCodeAsync(CancellationToken ct = default);

        /// <summary>6 أرقام CSPRNG (100000..999999 كلها صالحة).</summary>
        string NewAccessCode();
    }
}
