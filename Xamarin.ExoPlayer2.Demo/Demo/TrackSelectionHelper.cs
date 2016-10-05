using System.Linq;
using Android.Annotation;
using Android.App;
using Android.Content;
using Android.Support.V4.Util;
using Android.Text;
using Android.Views;
using Android.Widget;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Source;
using Com.Google.Android.Exoplayer2.Trackselection;
using Com.Google.Android.Exoplayer2.Util;
using Java.Util;

using Droid = Android;

namespace Xamarin.ExoPlayer2.Demo.Demo
{
	public class TrackSelectionHelper : Java.Lang.Object, View.IOnClickListener,
										IDialogInterfaceOnClickListener
	{
		private static ITrackSelectionFactory _fixedFactory;
		private static ITrackSelectionFactory _randomFactory;

		private MappingTrackSelector _selector;
		private ITrackSelectionFactory _adaptiveVideoTrackSelectionFactory;

		private MappingTrackSelector.TrackInfo _trackInfo;
		private int _rendererIndex;
		private TrackGroupArray _trackGroups;
		private bool[] _trackGroupsAdaptive;
		private bool _isDisabled;
		private MappingTrackSelector.SelectionOverride _override;

		private CheckedTextView _disableView;
		private CheckedTextView _defaultView;
		private CheckedTextView _enableRandomAdaptationView;
		private CheckedTextView[][] _trackViews;

		static TrackSelectionHelper()
		{
			_fixedFactory = new FixedTrackSelection.Factory();
			_randomFactory = new RandomTrackSelection.Factory();
		}

		/**
		 * @param selector The track selector.
		 * @param adaptiveVideoTrackSelectionFactory A factory for adaptive video {@link TrackSelection}s,
		 *     or null if the selection helper should not support adaptive video.
		 */
		public TrackSelectionHelper(MappingTrackSelector selector,
			 ITrackSelectionFactory adaptiveVideoTrackSelectionFactory)
		{
			_selector = selector;
			_adaptiveVideoTrackSelectionFactory = adaptiveVideoTrackSelectionFactory;
		}

		/**
		 * Shows the selection dialog for a given renderer.
		 *
		 * @param activity The parent activity.
		 * @param title The dialog's title.
		 * @param trackInfo The current track information.
		 * @param rendererIndex The index of the renderer.
		 */
		public void ShowSelectionDialog(Activity activity, char[] title, MappingTrackSelector.TrackInfo trackInfo,
			int rendererIndex)
		{
			_trackInfo = trackInfo;
			_rendererIndex = rendererIndex;

			_trackGroups = trackInfo.GetTrackGroups(rendererIndex);
			_trackGroupsAdaptive = new bool[_trackGroups.Length];
			for (int i = 0; i < _trackGroups.Length; i++)
			{
				_trackGroupsAdaptive[i] = _adaptiveVideoTrackSelectionFactory != null
					&& trackInfo.GetAdaptiveSupport(rendererIndex, i, false)
								!= RendererCapabilities.AdaptiveNotSupported
					&& _trackGroups.Get(i).Length > 1;
			}
			_isDisabled = _selector.GetRendererDisabled(rendererIndex);

			_override = _selector.GetSelectionOverride(rendererIndex, _trackGroups);

			AlertDialog.Builder builder = new AlertDialog.Builder(activity);
			builder.SetTitle(title.ToString())
				   .SetView(BuildView(LayoutInflater.From(builder.Context)))
				.SetPositiveButton(Droid.Resource.String.Ok, this)
				.SetNegativeButton(Droid.Resource.String.Cancel, this)
				.Create()
				.Show();
		}

		//@SuppressLint("InflateParams")
		[SuppressLint()]
		private View BuildView(LayoutInflater inflater)
		{
			View view = inflater.Inflate(Resource.Layout.track_selection_dialog, null);
			ViewGroup root = (ViewGroup)view.FindViewById(Resource.Id.root);

			// View for disabling the renderer.
			_disableView = (CheckedTextView)inflater.Inflate(
				Droid.Resource.Layout.SimpleListItemSingleChoice, root, false);
			_disableView.SetText(Resource.String.selection_disabled);
			_disableView.Focusable = true;
			_disableView.SetOnClickListener(this);
			root.AddView(_disableView);

			// View for clearing the override to allow the selector to use its default selection logic.
			_defaultView = (CheckedTextView)inflater.Inflate(
				Droid.Resource.Layout.SimpleListItemSingleChoice, root, false);
			_defaultView.SetText(Resource.String.selection_default);
			_defaultView.Focusable = true;
			_defaultView.SetOnClickListener(this);
			root.AddView(inflater.Inflate(Resource.Layout.list_divider, root, false));
			root.AddView(_defaultView);

			// Per-track views.
			bool haveSupportedTracks = false;
			bool haveAdaptiveTracks = false;
			_trackViews = new CheckedTextView[_trackGroups.Length][];
			for (int groupIndex = 0; groupIndex < _trackGroups.Length; groupIndex++)
			{
				TrackGroup group = _trackGroups.Get(groupIndex);
				bool groupIsAdaptive = _trackGroupsAdaptive[groupIndex];
				haveAdaptiveTracks |= groupIsAdaptive;
				_trackViews[groupIndex] = new CheckedTextView[group.Length];
				for (int trackIndex = 0; trackIndex < group.Length; trackIndex++)
				{
					if (trackIndex == 0)
					{
						root.AddView(inflater.Inflate(Resource.Layout.list_divider, root, false));
					}
					int trackViewLayoutId = groupIsAdaptive ? Droid.Resource.Layout.SimpleListItemMultipleChoice
						: Droid.Resource.Layout.SimpleListItemSingleChoice;
					CheckedTextView trackView = (CheckedTextView)inflater.Inflate(
						trackViewLayoutId, root, false);
					string txt = BuildTrackName(group.GetFormat(trackIndex));
					trackView.SetText(txt, TextView.BufferType.Normal);
					if (_trackInfo.GetTrackFormatSupport(_rendererIndex, groupIndex, trackIndex)
						== RendererCapabilities.FormatHandled)
					{
						trackView.Focusable = true;
						trackView.Tag = Pair.Create(groupIndex, trackIndex);
						trackView.SetOnClickListener(this);
						haveSupportedTracks = true;
					}
					else {
						trackView.Focusable = false;
						trackView.Enabled = false;
					}
					_trackViews[groupIndex][trackIndex] = trackView;
					root.AddView(trackView);
				}
			}

			if (!haveSupportedTracks)
			{
				// Indicate that the default selection will be nothing.
				_defaultView.Text = Application.Context.GetString(Resource.String.selection_default_none);
			}
			else if (haveAdaptiveTracks)
			{
				// View for using random adaptation.
				_enableRandomAdaptationView = (CheckedTextView)inflater.Inflate(
					Droid.Resource.Layout.SimpleListItemMultipleChoice, root, false);
				_enableRandomAdaptationView.Text = Application.Context.GetString(Resource.String.enable_random_adaptation);
				_enableRandomAdaptationView.SetOnClickListener(this);
				root.AddView(inflater.Inflate(Resource.Layout.list_divider, root, false));
				root.AddView(_enableRandomAdaptationView);
			}

			UpdateViews();
			return view;
		}

		private void UpdateViews()
		{
			_disableView.Checked = _isDisabled;
			_defaultView.Checked = !_isDisabled && _override == null;
			for (int i = 0; i < _trackViews.Length; i++)
			{
				for (int j = 0; j < _trackViews[i].Length; j++)
				{
					_trackViews[i][j].Checked = _override != null && _override.GroupIndex == i
						&& _override.ContainsTrack(j);
				}
			}
			if (_enableRandomAdaptationView != null)
			{
				bool enableView = !_isDisabled && _override != null && _override.Length > 1;
				_enableRandomAdaptationView.Enabled = enableView;
				_enableRandomAdaptationView.Focusable = enableView;
				if (enableView)
				{
					_enableRandomAdaptationView.Checked = !_isDisabled
						&& _override.Factory is RandomTrackSelection.Factory;
				}
			}
		}

		private void SetOverride(int group, int[] tracks, bool enableRandomAdaptation)
		{
			ITrackSelectionFactory factory = tracks.Length == 1 ? _fixedFactory
			   : (enableRandomAdaptation ? _randomFactory : _adaptiveVideoTrackSelectionFactory);
			_override = new MappingTrackSelector.SelectionOverride(factory, group, tracks);
		}

		#region IDialogInterfaceOnClickListener implementation

		public void OnClick(IDialogInterface dialog, int which)
		{
			_selector.SetRendererDisabled(_rendererIndex, _isDisabled);
			if (_override != null)
			{
				_selector.SetSelectionOverride(_rendererIndex, _trackGroups, _override);
			}
			else {
				_selector.ClearSelectionOverrides(_rendererIndex);
			}
		}

		#endregion

		#region View.IOnClickListener implementation

		public void OnClick(View view)
		{
			if (view == _disableView)
			{
				_isDisabled = true;
				_override = null;
			}
			else if (view == _defaultView)
			{
				_isDisabled = false;
				_override = null;
			}
			else if (view == _enableRandomAdaptationView)
			{
				SetOverride(_override.GroupIndex, _override.Tracks.ToArray(), !_enableRandomAdaptationView.Checked);
			}
			else {
				_isDisabled = false;
				//@SuppressWarnings("unchecked")

				Pair tag = (Pair)view.Tag;
				int groupIndex = (int)tag.First;
				int trackIndex = (int)tag.Second;

				if (!_trackGroupsAdaptive[groupIndex] || _override == null
					|| _override.GroupIndex != groupIndex)
				{
					_override = new MappingTrackSelector.SelectionOverride(_fixedFactory, groupIndex, trackIndex);
				}
				else {
					// The group being modified is adaptive and we already have a non-null override.
					bool isEnabled = ((CheckedTextView)view).Checked;
					int overrideLength = _override.Length;
					if (isEnabled)
					{
						// Remove the track from the override.
						if (overrideLength == 1)
						{
							// The last track is being removed, so the override becomes empty.
							_override = null;
							_isDisabled = true;
						}
						else {
							SetOverride(groupIndex, GetTracksRemoving(_override, trackIndex),
								_enableRandomAdaptationView.Checked);
						}
					}
					else {
						// Add the track to the override.
						SetOverride(groupIndex, GetTracksAdding(_override, trackIndex),
									_enableRandomAdaptationView.Checked);
					}
				}
			}
			// Update the views with the new state.
			UpdateViews();
		}

		#endregion

		#region Track array manipulation.

		private static int[] GetTracksAdding(MappingTrackSelector.SelectionOverride _override, int addedTrack)
		{
			int[] tracks = _override.Tracks.ToArray();
			tracks = Arrays.CopyOf(tracks, tracks.Length + 1);
			tracks[tracks.Length - 1] = addedTrack;
			return tracks;
		}

		private static int[] GetTracksRemoving(MappingTrackSelector.SelectionOverride _override, int removedTrack)
		{
			int[] tracks = new int[_override.Length - 1];
			int trackCount = 0;
			int[] overrideTracks = _override.Tracks.ToArray();
			for (int i = 0; i < tracks.Length + 1; i++)
			{
				int track = overrideTracks[i];
				if (track != removedTrack)
				{
					tracks[trackCount++] = track;
				}
			}
			return tracks;
		}

		#endregion

		#region Track name construction.

		private static string BuildTrackName(Format format)
		{
			string trackName;
			if (MimeTypes.IsVideo(format.SampleMimeType))
			{
				trackName = JoinWithSeparator(JoinWithSeparator(BuildResolutionString(format),
					BuildBitrateString(format)), BuildTrackIdString(format));
			}
			else if (MimeTypes.IsAudio(format.SampleMimeType))
			{
				trackName = JoinWithSeparator(JoinWithSeparator(JoinWithSeparator(BuildLanguageString(format),
					BuildAudioPropertyString(format)), BuildBitrateString(format)),
					BuildTrackIdString(format));
			}
			else {
				trackName = JoinWithSeparator(JoinWithSeparator(BuildLanguageString(format),
					BuildBitrateString(format)), BuildTrackIdString(format));
			}
			return trackName.Length == 0 ? "unknown" : trackName;
		}

		private static string BuildResolutionString(Format format)
		{
			return format.Width == Format.NoValue || format.Height == Format.NoValue
				? "" : format.Width + "x" + format.Height;
		}

		private static string BuildAudioPropertyString(Format format)
		{
			return format.ChannelCount == Format.NoValue || format.SampleRate == Format.NoValue
						 ? "" : format.ChannelCount + "ch, " + format.SampleRate + "Hz";
		}

		private static string BuildLanguageString(Format format)
		{
			return TextUtils.IsEmpty(format.Language) || "und".Equals(format.Language) ? ""
				: format.Language;
		}

		private static string BuildBitrateString(Format format)
		{
			return format.Bitrate == Format.NoValue ? ""
				: Locale.Us + "Mbit" + format.Bitrate / 1000000f;
		}

		private static string JoinWithSeparator(string first, string second)
		{
			return first.Length == 0 ? second : (second.Length == 0 ? first : first + ", " + second);
		}

		private static string BuildTrackIdString(Format format)
		{
			return format.Id == null ? "" : ("id:" + format.Id);
		}

		#endregion
	}
}
