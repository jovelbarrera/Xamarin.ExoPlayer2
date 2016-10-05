using Android.OS;
using Android.Util;
using Android.Views;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Audio;
using Com.Google.Android.Exoplayer2.Decoder;
using Com.Google.Android.Exoplayer2.Drm;
using Com.Google.Android.Exoplayer2.Metadata;
using Com.Google.Android.Exoplayer2.Source;
using Com.Google.Android.Exoplayer2.Trackselection;
using Com.Google.Android.Exoplayer2.Upstream;
using Com.Google.Android.Exoplayer2.Video;
using Java.IO;
using Java.Lang;
using Java.Text;
using Java.Util;

namespace Xamarin.ExoPlayer2.Demo.Demo
{
	public class EventLogger : Java.Lang.Object, IExoPlayerEventListener,
								IAudioRendererEventListener, IVideoRendererEventListener, IAdaptiveMediaSourceEventListener,
								ExtractorMediaSource.IEventListener, StreamingDrmSessionManager.IEventListener,
								MappingTrackSelector.IEventListener, MetadataRenderer.IOutput
	{
		private readonly string Tag = "EventLogger";
		private readonly int MaxTimelineItemLines = 3;
		private static readonly NumberFormat TimeFormat;

		private Timeline.Window _window;
		private Timeline.Period _period;
		private long _startTimeMs;

		static EventLogger()
		{
			TimeFormat = NumberFormat.GetInstance(Locale.Us);
			TimeFormat.MinimumFractionDigits = 2;
			TimeFormat.MaximumFractionDigits = 2;
			TimeFormat.GroupingUsed = false;
		}

		public EventLogger()
		{
			_window = new Timeline.Window();
			_period = new Timeline.Period();
			_startTimeMs = SystemClock.ElapsedRealtime();
		}

		#region IExoPlayerEventListener implementation

		public void OnLoadingChanged(bool isLoading)
		{
			Log.Debug(Tag, "loading [" + isLoading + "]");
		}


		public void OnPlayerStateChanged(bool playWhenReady, int state)
		{
			Log.Debug(Tag, "state [" + GetSessionTimestring() + ", " + playWhenReady + ", "
				+ GetStatestring(state) + "]");
		}

		public void OnPositionDiscontinuity()
		{
			Log.Debug(Tag, "positionDiscontinuity");
		}

		public void OnTimelineChanged(Timeline timeline, Java.Lang.Object manifest)
		{
			int periodCount = timeline.PeriodCount;
			int windowCount = timeline.WindowCount;
			Log.Debug(Tag, "sourceInfo [periodCount=" + periodCount + ", windowCount=" + windowCount);
			for (int i = 0; i < System.Math.Min(periodCount, MaxTimelineItemLines); i++)
			{
				timeline.GetPeriod(i, _period);
				Log.Debug(Tag, "  " + "period [" + GetTimestring(_period.DurationMs) + "]");
			}
			if (periodCount > MaxTimelineItemLines)
			{
				Log.Debug(Tag, "  ...");
			}
			for (int i = 0; i < System.Math.Min(windowCount, MaxTimelineItemLines); i++)
			{
				timeline.GetWindow(i, _window);
				Log.Debug(Tag, "  " + "window [" + GetTimestring(_window.DurationMs) + ", "
					+ _window.IsSeekable + ", " + _window.IsDynamic + "]");
			}
			if (windowCount > MaxTimelineItemLines)
			{
				Log.Debug(Tag, "  ...");
			}
			Log.Debug(Tag, "]");
		}

		public void OnPlayerError(ExoPlaybackException e)
		{
			Log.Error(Tag, "playerFailed [" + GetSessionTimestring() + "]", e);
		}
		#endregion

		#region MappingTrackSelector.IEventListener implementation

		public void OnTracksChanged(MappingTrackSelector.TrackInfo trackSelections)
		{
			/*Log.Debug(TAG, "Tracks [");
			// Log tracks associated to renderers.
			MappingTrackSelector.TrackInfo info = trackSelections;
			for (int rendererIndex = 0; rendererIndex < trackSelections.Length; rendererIndex++)
			{
				TrackGroupArray trackGroups = info.getTrackGroups(rendererIndex);
				TrackSelection trackSelection = trackSelections.get(rendererIndex);
				if (trackGroups.Length > 0)
				{
					Log.Debug(TAG, "  Renderer:" + rendererIndex + " [");
					for (int groupIndex = 0; groupIndex < trackGroups.Length; groupIndex++)
					{
						TrackGroup trackGroup = trackGroups.Get(groupIndex);
						string adaptiveSupport = GetAdaptiveSupportstring(
							trackGroup.Length, info.getAdaptiveSupport(rendererIndex, groupIndex, false));
						Log.Debug(TAG, "    Group:" + groupIndex + ", adaptive_supported=" + adaptiveSupport + " [");
						for (int trackIndex = 0; trackIndex < trackGroup.Length; trackIndex++)
						{
							string status = GetTrackStatusstring(trackSelection, trackGroup, trackIndex);
							string formatSupport = GetFormatSupportstring(
								info.getTrackFormatSupport(rendererIndex, groupIndex, trackIndex));
							Log.Debug(TAG, "      " + status + " Track:" + trackIndex + ", "
								+ GetFormatstring(trackGroup.GetFormat(trackIndex))
								+ ", supported=" + formatSupport);
						}
						Log.Debug(TAG, "    ]");
					}
					Log.Debug(TAG, "  ]");
				}
			}
			// Log tracks not associated with a renderer.
			TrackGroupArray trackGroups = info.getUnassociatedTrackGroups();
			if (trackGroups.Length > 0)
			{
				Log.Debug(TAG, "  Renderer:None [");
				for (int groupIndex = 0; groupIndex < trackGroups.Length; groupIndex++)
				{
					Log.Debug(TAG, "    Group:" + groupIndex + " [");
					TrackGroup trackGroup = trackGroups.get(groupIndex);
					for (int trackIndex = 0; trackIndex < trackGroup.Length; trackIndex++)
					{
						string status = GetTrackStatusstring(false);
						string formatSupport = GetFormatSupportstring(
							RendererCapabilities.FormatUnsupportedType);
						Log.Debug(TAG, "      " + status + " Track:" + trackIndex + ", "
											+ GetFormatstring(trackGroup.GetFormat(trackIndex))
											+ ", supported=" + formatSupport);
					}
					Log.Debug(TAG, "    ]");
				}
				Log.Debug(TAG, "  ]");
			}
			Log.Debug(TAG, "]");*/
		}

		#endregion

		#region  MetadataRenderer.IOutput implementation

		public void OnMetadata(Java.Lang.Object obj)
		{
			/*var id3Frames = obj as List<Id3Frame>;
			foreach (Id3Frame id3Frame in id3Frames)
			{
				if (id3Frame is TxxxFrame)
				{
					TxxxFrame txxxFrame = (TxxxFrame)id3Frame;
					Log.Info(TAG, string.Format("ID3 TimedMetadata %s: description=%s, value=%s", txxxFrame.Id,
						txxxFrame.Description, txxxFrame.Value));
				}
				else if (id3Frame is PrivFrame)
				{
					PrivFrame privFrame = (PrivFrame)id3Frame;
					Log.Info(TAG, string.Format("ID3 TimedMetadata %s: owner=%s", privFrame.Id, privFrame.Owner));
				}
				else if (id3Frame is GeobFrame)
				{
					GeobFrame geobFrame = (GeobFrame)id3Frame;
					Log.Info(TAG, string.Format("ID3 TimedMetadata %s: mimeType=%s, filename=%s, description=%s",
						geobFrame.id, geobFrame.MimeType, geobFrame.Filename, geobFrame.Description));
				}
				else if (id3Frame is ApicFrame)
				{
					ApicFrame apicFrame = (ApicFrame)id3Frame;
					Log.Info(TAG, string.Format("ID3 TimedMetadata %s: mimeType=%s, description=%s",
						apicFrame.id, apicFrame.MimeType, apicFrame.Description));
				}
				else if (id3Frame is TextInformationFrame)
				{
					TextInformationFrame textInformationFrame = (TextInformationFrame)id3Frame;
					Log.Info(TAG, string.Format("ID3 TimedMetadata %s: description=%s", textInformationFrame.Id,
						textInformationFrame.Description));
				}
				else {
					Log.Info(TAG, string.Format("ID3 TimedMetadata %s", id3Frame.Id));
				}
			}*/
		}

		#endregion

		#region IAudioRendererEventListener implementation

		public void OnAudioEnabled(DecoderCounters counters)
		{
			Log.Debug(Tag, "audioEnabled [" + GetSessionTimestring() + "]");
		}

		public void OnAudioSessionId(int audioSessionId)
		{
			Log.Debug(Tag, "audioSessionId [" + audioSessionId + "]");
		}

		public void OnAudioDecoderInitialized(string decoderName, long elapsedRealtimeMs,
			long initializationDurationMs)
		{
			Log.Debug(Tag, "audioDecoderInitialized [" + GetSessionTimestring() + ", " + decoderName + "]");
		}

		public void OnAudioInputFormatChanged(Format format)
		{
			Log.Debug(Tag, "audioFormatChanged [" + GetSessionTimestring() + ", " + GetFormatstring(format)
				+ "]");
		}

		public void OnAudioDisabled(DecoderCounters counters)
		{
			Log.Debug(Tag, "audioDisabled [" + GetSessionTimestring() + "]");
		}

		public void OnAudioTrackUnderrun(int bufferSize, long bufferSizeMs, long elapsedSinceLastFeedMs)
		{
			PrintInternalError("audioTrackUnderrun [" + bufferSize + ", " + bufferSizeMs + ", "
				+ elapsedSinceLastFeedMs + "]", null);
		}

		#endregion

		#region IVideoRendererEventListener implementation

		public void OnVideoEnabled(DecoderCounters counters)
		{
			Log.Debug(Tag, "videoEnabled [" + GetSessionTimestring() + "]");
		}

		public void OnVideoDecoderInitialized(string decoderName, long elapsedRealtimeMs,
			long initializationDurationMs)
		{
			Log.Debug(Tag, "videoDecoderInitialized [" + GetSessionTimestring() + ", " + decoderName + "]");
		}

		public void OnVideoInputFormatChanged(Format format)
		{
			Log.Debug(Tag, "videoFormatChanged [" + GetSessionTimestring() + ", " + GetFormatstring(format)
				+ "]");
		}

		public void OnVideoDisabled(DecoderCounters counters)
		{
			Log.Debug(Tag, "videoDisabled [" + GetSessionTimestring() + "]");
		}

		public void OnDroppedFrames(int count, long elapsed)
		{
			Log.Debug(Tag, "droppedFrames [" + GetSessionTimestring() + ", " + count + "]");
		}

		public void OnVideoSizeChanged(int width, int height, int unappliedRotationDegrees,
			float pixelWidthHeightRatio)
		{
			// Do nothing.
		}

		public void OnRenderedFirstFrame(Surface surface)
		{
			// Do nothing.
		}

		#endregion

		#region StreamingDrmSessionManager.IEventListener implementation

		public void OnDrmSessionManagerError(Java.Lang.Exception e)
		{
			PrintInternalError("drmSessionManagerError", e);
		}

		public void OnDrmKeysLoaded()
		{
			Log.Debug(Tag, "drmKeysLoaded [" + GetSessionTimestring() + "]");
		}

		#endregion

		#region ExtractorMediaSource.IEventListener implementation

		public void OnLoadError(IOException error)
		{
			PrintInternalError("loadError", error);
		}

		#endregion

		#region IAdaptiveMediaSourceEventListener implementation

		public void OnLoadStarted(DataSpec dataSpec, int dataType, int trackType, Format trackFormat,
			int trackSelectionReason, Java.Lang.Object trackSelectionData, long mediaStartTimeMs,
			long mediaEndTimeMs, long elapsedRealtimeMs)
		{
			// Do nothing.
		}

		public void OnLoadError(DataSpec dataSpec, int dataType, int trackType, Format trackFormat,
			int trackSelectionReason, Java.Lang.Object trackSelectionData, long mediaStartTimeMs,
			long mediaEndTimeMs, long elapsedRealtimeMs, long loadDurationMs, long bytesLoaded,
			IOException error, bool wasCanceled)
		{
			PrintInternalError("loadError", error);
		}

		public void OnLoadCanceled(DataSpec dataSpec, int dataType, int trackType, Format trackFormat,
			int trackSelectionReason, Java.Lang.Object trackSelectionData, long mediaStartTimeMs,
			long mediaEndTimeMs, long elapsedRealtimeMs, long loadDurationMs, long bytesLoaded)
		{
			// Do nothing.
		}

		public void OnLoadCompleted(DataSpec dataSpec, int dataType, int trackType, Format trackFormat,
			int trackSelectionReason, Java.Lang.Object trackSelectionData, long mediaStartTimeMs,
			long mediaEndTimeMs, long elapsedRealtimeMs, long loadDurationMs, long bytesLoaded)
		{
			// Do nothing.
		}

		public void OnUpstreamDiscarded(int trackType, long mediaStartTimeMs, long mediaEndTimeMs)
		{
			// Do nothing.
		}

		public void OnDownstreamFormatChanged(int trackType, Format trackFormat, int trackSelectionReason,
			Java.Lang.Object trackSelectionData, long mediaTimeMs)
		{
			// Do nothing.
		}

		#endregion

		#region Internal methods implementation

		private void PrintInternalError(string type, Java.Lang.Exception e)
		{
			Log.Error(Tag, "internalError [" + GetSessionTimestring() + ", " + type + "]", e);
		}

		private string GetSessionTimestring()
		{
			return GetTimestring(SystemClock.ElapsedRealtime() - _startTimeMs);
		}

		private static string GetTimestring(long timeMs)
		{
			return timeMs == C.TimeUnset ? "?" : TimeFormat.Format((timeMs) / 1000f);
		}

		private static string GetStatestring(int state)
		{
			switch (state)
			{
				case Com.Google.Android.Exoplayer2.ExoPlayer.StateBuffering:
					return "B";
				case Com.Google.Android.Exoplayer2.ExoPlayer.StateEnded:
					return "E";
				case Com.Google.Android.Exoplayer2.ExoPlayer.StateIdle:
					return "I";
				case Com.Google.Android.Exoplayer2.ExoPlayer.StateReady:
					return "R";
				default:
					return "?";
			}
		}

		private static string GetFormatSupportstring(int formatSupport)
		{
			switch (formatSupport)
			{
				case RendererCapabilities.FormatHandled:
					return "YES";
				case RendererCapabilities.FormatExceedsCapabilities:
					return "NO_EXCEEDS_CAPABILITIES";
				case RendererCapabilities.FormatUnsupportedSubtype:
					return "NO_UNSUPPORTED_TYPE";
				case RendererCapabilities.FormatUnsupportedType:
					return "NO";
				default:
					return "?";
			}
		}

		private static string GetAdaptiveSupportstring(int trackCount, int adaptiveSupport)
		{
			if (trackCount < 2)
			{
				return "N/A";
			}
			switch (adaptiveSupport)
			{
				case RendererCapabilities.AdaptiveSeamless:
					return "YES";
				case RendererCapabilities.AdaptiveNotSeamless:
					return "YES_NOT_SEAMLESS";
				case RendererCapabilities.AdaptiveNotSupported:
					return "NO";
				default:
					return "?";
			}
		}

		private static string GetFormatstring(Format format)
		{
			if (format == null)
			{
				return "null";
			}
			StringBuilder builder = new StringBuilder();
			builder.Append("id=").Append(format.Id).Append(", mimeType=").Append(format.SampleMimeType);
			if (format.Bitrate != Format.NoValue)
			{
				builder.Append(", bitrate=").Append(format.Bitrate.ToString());
			}
			if (format.Width != Format.NoValue && format.Height != Format.NoValue)
			{
				builder.Append(", res=").Append(format.Width.ToString()).Append("x").Append(format.Height.ToString());
			}
			if (format.FrameRate != Format.NoValue)
			{
				builder.Append(", fps=").Append(format.FrameRate.ToString());
			}
			if (format.ChannelCount != Format.NoValue)
			{
				builder.Append(", channels=").Append(format.ChannelCount.ToString());
			}
			if (format.SampleRate != Format.NoValue)
			{
				builder.Append(", sample_rate=").Append(format.SampleRate.ToString());
			}
			if (format.Language != null)
			{
				builder.Append(", language=").Append(format.Language);
			}
			return builder.ToString();
		}

		/*private static string GetTrackStatusString(TrackSelection selection, TrackGroup group,
			int trackIndex)
		{
			return GetTrackStatusString(selection != null && selection.getTrackGroup() == group
										&& selection.indexOf(trackIndex) != C.IndexUnset);
		}

		private static string GetTrackStatusString(bool enabled)
		{
			return enabled ? "[X]" : "[ ]";
		}*/

		#endregion
	}
}