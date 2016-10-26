using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Text;
using Android.Views;
using Android.Widget;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Drm;
using Com.Google.Android.Exoplayer2.Extractor;
using Com.Google.Android.Exoplayer2.Source;
using Com.Google.Android.Exoplayer2.Source.Dash;
using Com.Google.Android.Exoplayer2.Source.Hls;
using Com.Google.Android.Exoplayer2.Source.Smoothstreaming;
using Com.Google.Android.Exoplayer2.Text;
using Com.Google.Android.Exoplayer2.Trackselection;
using Com.Google.Android.Exoplayer2.UI;
using Com.Google.Android.Exoplayer2.Upstream;
using Com.Google.Android.Exoplayer2.Util;
using Java.Lang;
using Java.Net;
using Java.Util;

namespace Xamarin.ExoPlayer2.Demo.Demo
{
	[Activity]
	public class PlayerActivity : Activity, View.IOnClickListener, IExoPlayerEventListener,
	MappingTrackSelector.IEventListener, PlaybackControlView.IVisibilityListener, TextRenderer.IOutput
	{
		public static readonly string DrmSchemeUuidExtra = "drm_scheme_uuid";
		public static readonly string DrmLicenseUrl = "drm_license_url";
		public static readonly string DrmKeyRequestProperties = "drm_key_request_properties";
		public static readonly string PreferExtensionDecoders = "prefer_extension_decoders";

		public static readonly string ActionView = "com.google.android.exoplayer.demo.action.VIEW";
		public static readonly string ExtensionExtra = "extension";

		public static readonly string ActionViewList = "com.google.android.exoplayer.demo.action.VIEW_LIST";
		public static readonly string UriListExtra = "uri_list";
		public static readonly string ExtensionListExtra = "extension_list";

		private static DefaultBandwidthMeter _bandwidthMeter;
		private static CookieManager _defaultCookieManager;

		private Handler _mainHandler;
		private EventLogger _eventLogger;
		private SimpleExoPlayerView _simpleExoPlayerView;
		private LinearLayout _debugRootView;
		private TextView _debugTextView;
		private Button _retryButton;

		private IDataSourceFactory _mediaDataSourceFactory;
		private SimpleExoPlayer _player;
		private MappingTrackSelector _trackSelector;
		private TrackSelectionHelper _trackSelectionHelper;
		private DebugTextViewHelper _debugViewHelper;
		private bool _playerNeedsSource;

		private bool _shouldAutoPlay;
		private bool _shouldRestorePosition;
		private int _playerWindow;
		private long _playerPosition;

		protected string UserAgent;

		static PlayerActivity()
		{
			_bandwidthMeter = new DefaultBandwidthMeter();
			_defaultCookieManager = new CookieManager();
			_defaultCookieManager.SetCookiePolicy(CookiePolicy.AcceptOriginalServer);
		}

		#region Activity lifecycle

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			UserAgent = Util.GetUserAgent(this, "ExoPlayerDemo");

			_shouldAutoPlay = true;
			_mediaDataSourceFactory = BuildDataSourceFactory(true);
			_mainHandler = new Handler();

			//if (CookieHandler.Default != _defaultCookieManager)
			//{
				CookieHandler.Default = _defaultCookieManager;
			//}

			SetContentView(Resource.Layout.player_activity);
			View rootView = FindViewById(Resource.Id.root);
			rootView.SetOnClickListener(this);
			_debugRootView = (LinearLayout)FindViewById(Resource.Id.controls_root);
			_debugTextView = (TextView)FindViewById(Resource.Id.debug_text_view);
			_retryButton = (Button)FindViewById(Resource.Id.retry_button);
			_retryButton.SetOnClickListener(this);

			_simpleExoPlayerView = (SimpleExoPlayerView)FindViewById(Resource.Id.player_view);
			_simpleExoPlayerView.SetControllerVisibilityListener(this);
			//_simpleExoPlayerView.SetUseController(false);
			_simpleExoPlayerView.RequestFocus();
		}

		protected override void OnNewIntent(Intent intent)
		{
			ReleasePlayer();
			_shouldRestorePosition = false;
			Intent = intent;
		}

		protected override void OnStart()
		{
			base.OnStart();
			if (Util.SdkInt > 23)
			{
				InitializePlayer();
			}
		}

		protected override void OnResume()
		{
			base.OnResume();
			if ((Util.SdkInt <= 23 || _player == null))
			{
				InitializePlayer();
			}
		}

		protected override void OnPause()
		{
			base.OnPause();
			if (Util.SdkInt <= 23)
			{
				ReleasePlayer();
			}
		}

		protected override void OnStop()
		{
			base.OnStop();
			if (Util.SdkInt > 23)
			{
				ReleasePlayer();
			}
		}

		#endregion

		public void OnRequestPermissionsResult(int requestCode, string[] permissions, int[] grantResults)
		{
			if (grantResults.Length > 0 && grantResults[0] == (int)Permission.Granted)
			{
				InitializePlayer();
			}
			else {
				ShowToast(Resource.String.storage_permission_denied);
				//finish();
			}
		}

		#region View.IOnClickListener implementation

		public void OnClick(View view)
		{
			if (view == _retryButton)
			{
				InitializePlayer();
			}
			else if (view.Parent == _debugRootView)
			{
				//trackSelectionHelper.showSelectionDialog(this, ((Button)view).Text, trackSelector.getCurrentSelections().info, (int)view.Tag);
			}
		}

		#endregion

		#region PlaybackControlView.IVisibilityListener implementation

		public void OnVisibilityChange(int visibility)
		{
			_debugRootView.Visibility = (ViewStates)visibility;
		}

		#endregion

		#region  Internal methods

		private void InitializePlayer()
		{
			Intent intent = Intent;
			if (_player == null)
			{
				bool preferExtensionDecoders = intent.GetBooleanExtra(PreferExtensionDecoders, false);
				UUID drmSchemeUuid = intent.HasExtra(DrmSchemeUuidExtra)
										   ? UUID.FromString(intent.GetStringExtra(DrmSchemeUuidExtra)) : null;
				//DrmSessionManager<FrameworkMediaCrypto>
				IDrmSessionManager drmSessionManager = null;
				if (drmSchemeUuid != null)
				{
					string drmLicenseUrl = intent.GetStringExtra(DrmLicenseUrl);
					string[] keyRequestPropertiesArray = intent.GetStringArrayExtra(DrmKeyRequestProperties);
					Dictionary<string, string> keyRequestProperties;
					if (keyRequestPropertiesArray == null || keyRequestPropertiesArray.Length < 2)
					{
						keyRequestProperties = null;
					}
					else {
						keyRequestProperties = new Dictionary<string, string>();
						for (int i = 0; i < keyRequestPropertiesArray.Length - 1; i += 2)
						{
							keyRequestProperties.Add(keyRequestPropertiesArray[i],
								keyRequestPropertiesArray[i + 1]);
						}
					}
					try
					{
						drmSessionManager = BuildDrmSessionManager(drmSchemeUuid, drmLicenseUrl,
							keyRequestProperties);
					}
					catch (UnsupportedDrmException e)
					{
						int errorstringId = Util.SdkInt < 18 ? Resource.String.error_drm_not_supported
												: (e.Reason == UnsupportedDrmException.ReasonUnsupportedScheme
							? Resource.String.error_drm_unsupported_scheme : Resource.String.error_drm_unknown);
						//ShowTost(errorstringId);
						return;
					}
				}

				_eventLogger = new EventLogger();
				ITrackSelectionFactory videoTrackSelectionFactory =
					new AdaptiveVideoTrackSelection.Factory(_bandwidthMeter);
				_trackSelector = new DefaultTrackSelector(_mainHandler, videoTrackSelectionFactory);
				_trackSelector.AddListener(this);
				_trackSelector.AddListener(_eventLogger);
				_trackSelectionHelper = new TrackSelectionHelper(_trackSelector, videoTrackSelectionFactory);
				_player = ExoPlayerFactory.NewSimpleInstance(this, _trackSelector, new DefaultLoadControl(),
					/*drmSessionManager*/null, preferExtensionDecoders);
				_player.AddListener(this);
				_player.AddListener(_eventLogger);
				_player.SetAudioDebugListener(_eventLogger);
				_player.SetVideoDebugListener(_eventLogger);
				_player.SetId3Output(_eventLogger);
				_simpleExoPlayerView.SetPlayer(_player);
				if (_shouldRestorePosition)
				{
					if (_playerPosition == C.TimeUnset)
					{
						_player.SeekToDefaultPosition(_playerWindow);
					}
					else {
						_player.SeekTo(_playerWindow, _playerPosition);
					}
				}
				_player.PlayWhenReady = _shouldAutoPlay;
				_debugViewHelper = new DebugTextViewHelper(_player, _debugTextView);
				_debugViewHelper.Start();
				_playerNeedsSource = true;
			}
			if (_playerNeedsSource)
			{
				string action = intent.Action;
				global::Android.Net.Uri[] uris;
				string[] extensions;
				if (ActionView.Equals(action))
				{
					uris = new global::Android.Net.Uri[] { intent.Data };
					extensions = new string[] { intent.GetStringExtra(ExtensionExtra) };
				}
				else if (ActionViewList.Equals(action))
				{
					string[] uristrings = intent.GetStringArrayExtra(UriListExtra);
					uris = new global::Android.Net.Uri[uristrings.Length];
					for (int i = 0; i < uristrings.Length; i++)
					{
						uris[i] = global::Android.Net.Uri.Parse(uristrings[i]);
					}
					extensions = intent.GetStringArrayExtra(ExtensionListExtra);
					if (extensions == null)
					{
						extensions = new string[uristrings.Length];
					}
				}
				else
				{
					ShowToast(GetString(Resource.String.unexpected_intent_action, action));
					return;
				}
				if (Util.MaybeRequestReadExternalStoragePermission(this, uris))
				{
					// The player will be reinitialized if the permission is granted.
					return;
				}
				IMediaSource[] mediaSources = new IMediaSource[uris.Length];
				for (int i = 0; i < uris.Length; i++)
				{
					mediaSources[i] = BuildMediaSource(uris[i], extensions[i]);
				}
				IMediaSource mediaSource = mediaSources.Length == 1 ? mediaSources[0]
					: new ConcatenatingMediaSource(mediaSources);
				_player.Prepare(mediaSource, !_shouldRestorePosition);
				_playerNeedsSource = false;
				UpdateButtonVisibilities();
			}
		}

		private IMediaSource BuildMediaSource(global::Android.Net.Uri uri, string overrideExtension)
		{
			int type = Util.InferContentType(!TextUtils.IsEmpty(overrideExtension) ? "." + overrideExtension
											 : uri.LastPathSegment);
			switch (type)
			{
				case Util.TypeSs:
					return new SsMediaSource(uri, BuildDataSourceFactory(false),
						new DefaultSsChunkSource.Factory(_mediaDataSourceFactory), _mainHandler, _eventLogger);
				case Util.TypeDash:
					return new DashMediaSource(uri, BuildDataSourceFactory(false),
						new DefaultDashChunkSource.Factory(_mediaDataSourceFactory), _mainHandler, _eventLogger);
				case Util.TypeHls:
					return new HlsMediaSource(uri, _mediaDataSourceFactory, _mainHandler, _eventLogger);
				case Util.TypeOther:
					return new ExtractorMediaSource(uri, _mediaDataSourceFactory, new DefaultExtractorsFactory(),
						_mainHandler, _eventLogger);
				default:
					{
						throw new IllegalStateException("Unsupported type: " + type);
					}
			}
		}

		/*DrmSessionManager<FrameworkMediaCrypto>*/
		private IDrmSessionManager BuildDrmSessionManager(UUID uuid, string licenseUrl, Dictionary<string, string> keyRequestProperties)
		{
			if (Util.SdkInt < 18)
			{
				return null;
			}
			HttpMediaDrmCallback drmCallback = new HttpMediaDrmCallback(licenseUrl,
				BuildHttpDataSourceFactory(false), keyRequestProperties);
			return new StreamingDrmSessionManager(uuid,
				FrameworkMediaDrm.NewInstance(uuid), drmCallback, null, _mainHandler, _eventLogger);
		}

		private void ReleasePlayer()
		{
			if (_player != null)
			{
				_debugViewHelper.Stop();
				_debugViewHelper = null;
				_shouldAutoPlay = _player.PlayWhenReady;
				_shouldRestorePosition = false;
				Timeline timeline = _player.CurrentTimeline;
				if (timeline != null)
				{
					_playerWindow = _player.CurrentWindowIndex;
					Timeline.Window window = timeline.GetWindow(_playerWindow, new Timeline.Window());
					if (!window.IsDynamic)
					{
						_shouldRestorePosition = true;
						_playerPosition = window.IsSeekable ? _player.CurrentPosition : C.TimeUnset;
					}
				}
				_player.Release();
				_player = null;
				_trackSelector = null;
				_trackSelectionHelper = null;
				_eventLogger = null;
			}
		}

		/**
		 * Returns a new DataSource factory.
		 *
		 * @param useBandwidthMeter Whether to set {@link #BANDWIDTH_METER} as a listener to the new
		 *     DataSource factory.
		 * @return A new DataSource factory.
		 */
		private IDataSourceFactory BuildDataSourceFactory(bool useBandwidthMeter)
		{
			return BuildDataSourceFactory(useBandwidthMeter ? _bandwidthMeter : null);
		}

		public IDataSourceFactory BuildDataSourceFactory(DefaultBandwidthMeter bandwidthMeter)
		{
			return new DefaultDataSourceFactory(this, bandwidthMeter, BuildHttpDataSourceFactory(bandwidthMeter));
		}

		/**
		 * Returns a new HttpDataSource factory.
		 *
		 * @param useBandwidthMeter Whether to set {@link #BANDWIDTH_METER} as a listener to the new
		 *     DataSource factory.
		 * @return A new HttpDataSource factory.
		 */
		private IHttpDataSourceFactory BuildHttpDataSourceFactory(bool useBandwidthMeter)
		{
			return BuildHttpDataSourceFactory(useBandwidthMeter ? _bandwidthMeter : null);
		}

		public IHttpDataSourceFactory BuildHttpDataSourceFactory(DefaultBandwidthMeter bandwidthMeter)
		{
			return new DefaultHttpDataSourceFactory(UserAgent, bandwidthMeter);
		}

		#endregion

		#region IExoPlayerEventListener implementation

		public void OnLoadingChanged(bool isLoading)
		{
			// Do nothing.
		}

		public void OnPlayerStateChanged(bool playWhenReady, int playbackState)
		{
			if (playbackState == Com.Google.Android.Exoplayer2.ExoPlayer.StateEnded)
			{
				ShowControls();
			}
			UpdateButtonVisibilities();
		}

		public void OnPositionDiscontinuity()
		{
			// Do nothing.
		}

		public void OnTimelineChanged(Timeline timeline, Java.Lang.Object manifest)
		{
			// Do nothing.
		}

		public void OnPlayerError(ExoPlaybackException e)
		{
			string errorstring = null;
			if (e.Type == ExoPlaybackException.TypeRenderer)
			{
				Java.Lang.Exception cause = e.RendererException;
				if (cause is Com.Google.Android.Exoplayer2.Mediacodec.MediaCodecRenderer.DecoderInitializationException)
				{
					// Special case for decoder initialization failures.
					Com.Google.Android.Exoplayer2.Mediacodec.MediaCodecRenderer.DecoderInitializationException decoderInitializationException =
						(Com.Google.Android.Exoplayer2.Mediacodec.MediaCodecRenderer.DecoderInitializationException)cause;
					if (decoderInitializationException.DecoderName == null)
					{
						if (decoderInitializationException.Cause is Com.Google.Android.Exoplayer2.Mediacodec.MediaCodecUtil.DecoderQueryException)
						{
							errorstring = GetString(Resource.String.error_querying_decoders);
						}
						else if (decoderInitializationException.SecureDecoderRequired)
						{
							errorstring = GetString(Resource.String.error_no_secure_decoder,
								decoderInitializationException.MimeType);
						}
						else {
							errorstring = GetString(Resource.String.error_no_decoder,
								decoderInitializationException.MimeType);
						}
					}
					else {
						errorstring = GetString(Resource.String.error_instantiating_decoder,
							decoderInitializationException.DecoderName);
					}
				}
			}
			if (errorstring != null)
			{
				ShowToast(errorstring);
			}
			_playerNeedsSource = true;
			UpdateButtonVisibilities();
			ShowControls();
		}

		#endregion

		#region MappingTrackSelector.IEventListener implementation

		public void OnTracksChanged(MappingTrackSelector.TrackInfo trackSelections)
		{
			UpdateButtonVisibilities();
			MappingTrackSelector.TrackInfo trackInfo = trackSelections;
			if (trackInfo.HasOnlyUnplayableTracks(C.TrackTypeVideo))
			{
				ShowToast(Resource.String.error_unsupported_video);
			}
			if (trackInfo.HasOnlyUnplayableTracks(C.TrackTypeAudio))
			{
				ShowToast(Resource.String.error_unsupported_audio);
			}
		}

		#endregion

		#region User controls

		private void UpdateButtonVisibilities()
		{
			_debugRootView.RemoveAllViews();

			_retryButton.Visibility = _playerNeedsSource ? ViewStates.Visible : ViewStates.Gone;
			_debugRootView.AddView(_retryButton);

			if (_player == null)
			{
				return;
			}

			/*TrackSelections<MappedTrackInfo> trackSelections = trackSelector.getCurrentSelections();

			if (trackSelections == null)
			{
				return;
			}

			int rendererCount = trackSelections.Length;
			for (int i = 0; i < rendererCount; i++)
			{
				TrackGroupArray trackGroups = trackSelections.info.getTrackGroups(i);
				if (trackGroups.Length != 0)
				{
					Button button = new Button(this);
					int label;
					switch (player.getRendererType(i))
					{
						case C.TrackTypeAudio:
							label = Resource.String.audio;
							break;
						case C.TrackTypeVideo:
							label = Resource.String.video;
							break;
						case C.TrackTypeText:
							label = Resource.String.text;
							break;
						default:
							continue;
					}
					button.Text = label.ToString();
					button.Tag = i;
					button.SetOnClickListener(this);
					debugRootView.AddView(button);
				}
			}*/
		}

		private void ShowControls()
		{
			_debugRootView.Visibility = ViewStates.Visible;
		}

		private void ShowToast(int messageId)
		{
			ShowToast(GetString(messageId));
		}

		private void ShowToast(string message)
		{
			Toast.MakeText(ApplicationContext, message, ToastLength.Long).Show();
		}

		public void OnCues(IList<Cue> p0)
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
