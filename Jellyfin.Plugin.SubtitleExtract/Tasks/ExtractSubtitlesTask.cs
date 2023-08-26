using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.SubtitleExtract.Tools;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleExtract.Tasks;

/// <summary>
/// Scheduled task to extract embedded subtitles for immediate access in web player.
/// </summary>
public class ExtractSubtitlesTask : IScheduledTask
{
    private const int QueryPageLimit = 100;

    private readonly ILibraryManager _libraryManager;
    private readonly ISubtitleEncoder _subtitleEncoder;
    private readonly ILocalizationManager _localization;
    private readonly ILoggerFactory _loggerFactory;

    private static readonly BaseItemKind[] _itemTypes = { BaseItemKind.Episode, BaseItemKind.Movie };
    private static readonly string[] _mediaTypes = { MediaType.Video };
    private static readonly SourceType[] _sourceTypes = { SourceType.Library };
    private static readonly DtoOptions _dtoOptions = new(false);

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractSubtitlesTask" /> class.
    /// </summary>
    /// <param name="libraryManager">Instance of <see cref="ILibraryManager"/> interface.</param>
    /// /// <param name="subtitleEncoder">Instance of <see cref="ISubtitleEncoder"/> interface.</param>
    /// <param name="localization">Instance of <see cref="ILocalizationManager"/> interface.</param>
    /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
    public ExtractSubtitlesTask(
        ILibraryManager libraryManager,
        ISubtitleEncoder subtitleEncoder,
        ILocalizationManager localization,
        ILoggerFactory loggerFactory)
    {
        _libraryManager = libraryManager;
        _subtitleEncoder = subtitleEncoder;
        _localization = localization;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public string Key => "ExtractSubtitles";

    /// <inheritdoc />
    public string Name => SubtitleExtractPlugin.Current!.Name;

    /// <inheritdoc />
    public string Description => SubtitleExtractPlugin.Current!.Description;

    /// <inheritdoc />
    public string Category => _localization.GetLocalizedString("TasksLibraryCategory");

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Enumerable.Empty<TaskTriggerInfo>();

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var query = new InternalItemsQuery
        {
            Recursive = true,
            HasSubtitles = true,
            IsVirtualItem = false,
            IncludeItemTypes = _itemTypes,
            DtoOptions = _dtoOptions,
            MediaTypes = _mediaTypes,
            SourceTypes = _sourceTypes,
            Limit = QueryPageLimit,
        };

        var numberOfVideos = _libraryManager.GetCount(query);

        var startIndex = 0;
        var completedVideos = 0;

        var extractor = new SubtitlesExtractor(_loggerFactory.CreateLogger<SubtitlesExtractor>(), _subtitleEncoder);

        while (startIndex < numberOfVideos)
        {
            query.StartIndex = startIndex;
            var videos = _libraryManager.GetItemList(query);

            foreach (var video in videos)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await extractor.Run(video, cancellationToken).ConfigureAwait(false);

                completedVideos++;
                progress.Report(100d * completedVideos / numberOfVideos);
            }

            startIndex += QueryPageLimit;
        }

        progress.Report(100);
    }
}
