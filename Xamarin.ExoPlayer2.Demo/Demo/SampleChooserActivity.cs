using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Upstream;
using Com.Google.Android.Exoplayer2.Util;
using Java.IO;
using Java.Lang;
using Java.Util;

using Droid = Android;

namespace Xamarin.ExoPlayer2.Demo.Demo
{
	[Activity(Label = "Xamarin.ExoPlayer2.Demo", MainLauncher = true, Icon = "@drawable/ic_launcher")]
	public class SampleChooserActivity : Activity, ExpandableListView.IOnChildClickListener
	{
		private static string TAG = "SampleChooserActivity";
		private string[] _uris;

		List<SampleGroup> _groups;

		private static SampleChooserActivity _instance;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			SetContentView(Resource.Layout.sample_chooser_activity);
			Intent intent = Intent;
			string dataUri = intent.DataString;

			if (dataUri != null)
			{
				_uris = new string[] { dataUri };
			}
			else
			{
				_uris = new string[] {
				  "asset:///media.exolist.json"
			  };
			}
			var loaderTask = new SampleListLoader();
			loaderTask.Execute(_uris);
			_instance = this;
		}

		private void OnSampleGroups(List<SampleGroup> groups, bool sawError)
		{
			_groups = groups;
			if (sawError)
			{
				Toast.MakeText(ApplicationContext, Resource.String.sample_list_load_error, ToastLength.Long)
				.Show();
			}

			var sampleList = (ExpandableListView)FindViewById(Resource.Id.sample_list);
			sampleList.SetAdapter(new SampleAdapter(this, groups));
			sampleList.SetOnChildClickListener(this);
		}

		private void OnSampleSelected(Sample sample)
		{
			StartActivity(sample.BuildIntent(this));
		}

		public bool OnChildClick(ExpandableListView parent, View clickedView, int groupPosition, int childPosition, long id)
		{
			OnSampleSelected(_groups.ElementAt(groupPosition).Samples.ElementAt(childPosition));
			return true;
		}

		public class SampleListLoader : AsyncTask<string, object, List<SampleGroup>>
		{
			private bool _sawError;

			protected override Java.Lang.Object DoInBackground(params Java.Lang.Object[] native_parms)
			{
				var result = new List<SampleGroup>();
				Context context = Application.Context;
				string userAgent = Util.GetUserAgent(context, "ExoPlayerDemo");
				IDataSource dataSource = new DefaultDataSource(context, null, userAgent, false);
				foreach (var uri in native_parms)
				{
					//var dataSpec = new DataSpec(Android.Net.Uri.Parse(uri.ToString()));
					//InputStream inputStream = new DataSourceInputStream(dataSource, dataSpec);

					System.IO.Stream inputStream = Application.Context.Assets.Open("media.exolist.json");

					try
					{
						ReadSampleGroups(new JsonReader(new InputStreamReader(inputStream, "UTF-8")), result);
					}
					catch (Java.Lang.Exception e)
					{
						Log.Error(TAG, "Error loading sample list: " + uri, e);
						_sawError = true;
					}
					finally
					{
						Util.CloseQuietly(dataSource);
					}
				}
				_instance.RunOnUiThread(() =>
				{
					_instance.OnSampleGroups(result, _sawError);
				});

				return default(Java.Lang.Object);
			}

			protected override void OnPostExecute(List<SampleGroup> result)
			{
				_instance.OnSampleGroups(result, _sawError);
			}

			private void ReadSampleGroups(JsonReader reader, List<SampleGroup> groups)
			{
				reader.BeginArray();
				while (reader.HasNext)
				{
					ReadSampleGroup(reader, groups);
				}
				reader.EndArray();
			}

			private void ReadSampleGroup(JsonReader reader, List<SampleGroup> groups)
			{
				string groupName = "";
				var samples = new List<Sample>();

				reader.BeginObject();
				while (reader.HasNext)
				{
					string name = reader.NextName();
					switch (name)
					{
						case "name":
							groupName = reader.NextString();
							break;
						case "samples":
							reader.BeginArray();
							while (reader.HasNext)
							{
								samples.Add(ReadEntry(reader, false));
							}
							reader.EndArray();
							break;
						case "_comment":
							reader.NextString(); // Ignore.
							break;
						default:
							throw new ParserException("Unsupported name: " + name);
					}
				}
				reader.EndObject();

				SampleGroup group = GetGroup(groupName, groups);
				group.Samples = samples;
			}

			private Sample ReadEntry(JsonReader reader, bool insidePlaylist)
			{
				string sampleName = null;
				string uri = null;
				string extension = null;
				UUID drmUuid = null;
				string drmLicenseUrl = null;
				string[] drmKeyRequestProperties = null;
				bool preferExtensionDecoders = false;
				List<UriSample> playlistSamples = null;

				reader.BeginObject();
				while (reader.HasNext)
				{
					string name = reader.NextName();
					switch (name)
					{
						case "name":
							sampleName = reader.NextString();
							break;
						case "uri":
							uri = reader.NextString();
							break;
						case "extension":
							extension = reader.NextString();
							break;
						case "drm_scheme":
							Assertions.CheckState(!insidePlaylist, "Invalid attribute on nested item: drm_scheme");
							drmUuid = GetDrmUuid(reader.NextString());
							break;
						case "drm_license_url":
							Assertions.CheckState(!insidePlaylist,
								"Invalid attribute on nested item: drm_license_url");
							drmLicenseUrl = reader.NextString();
							break;
						case "drm_key_request_properties":
							Assertions.CheckState(!insidePlaylist,
								"Invalid attribute on nested item: drm_key_request_properties");
							var drmKeyRequestPropertiesList = new List<string>();
							reader.BeginObject();
							while (reader.HasNext)
							{
								drmKeyRequestPropertiesList.Add(reader.NextName());
								drmKeyRequestPropertiesList.Add(reader.NextString());
							}
							reader.EndObject();
							drmKeyRequestProperties = drmKeyRequestPropertiesList.ToArray();
							break;
						case "prefer_extension_decoders":
							Assertions.CheckState(!insidePlaylist,
								"Invalid attribute on nested item: prefer_extension_decoders");
							preferExtensionDecoders = reader.NextBoolean();
							break;
						case "playlist":
							Assertions.CheckState(!insidePlaylist, "Invalid nesting of playlists");
							playlistSamples = new List<UriSample>();
							reader.BeginArray();
							while (reader.HasNext)
							{
								playlistSamples.Add((UriSample)ReadEntry(reader, true));
							}
							reader.EndArray();
							break;
						default:
							throw new ParserException("Unsupported attribute name: " + name);
					}
				}
				reader.EndObject();

				if (playlistSamples != null)
				{
					UriSample[] playlistSamplesArray = playlistSamples.ToArray();
					return new PlaylistSample(sampleName, drmUuid, drmLicenseUrl, drmKeyRequestProperties,
						preferExtensionDecoders, playlistSamplesArray);
				}
				else {
					return new UriSample(sampleName, drmUuid, drmLicenseUrl, drmKeyRequestProperties,
						preferExtensionDecoders, uri, extension);
				}
			}

			private SampleGroup GetGroup(string groupName, List<SampleGroup> groups)
			{
				for (int i = 0; i < groups.Count; i++)
				{
					if (Util.AreEqual(groupName, groups.ElementAt(i).Title))
					{
						return groups.ElementAt(i);
					}
				}
				var group = new SampleGroup(groupName);
				groups.Add(group);
				return group;
			}

			private UUID GetDrmUuid(string typestring)
			{
				switch (typestring.ToLower())
				{
					case "widevine":
						return C.WidevineUuid;
					case "playready":
						return C.PlayreadyUuid;
					default:
						try
						{
							return UUID.FromString(typestring);
						}
						catch (RuntimeException e)
						{
							var error = e.Message;
							throw new ParserException("Unsupported drm type: " + typestring);
						}
				}
			}

			protected override List<SampleGroup> RunInBackground(params string[] @params)
			{
				//throw new NotImplementedException();
				return null;
			}
		}

		private class SampleAdapter : BaseExpandableListAdapter
		{
			private Context _context;
			private List<SampleGroup> _sampleGroups;

			public SampleAdapter(Context context, List<SampleGroup> sampleGroups)
			{
				_context = context;
				_sampleGroups = sampleGroups;
			}

			public override Java.Lang.Object GetChild(int groupPosition, int childPosition)
			{
				return ((SampleGroup)GetGroup(groupPosition)).Samples.ElementAt(childPosition);
			}

			public override long GetChildId(int groupPosition, int childPosition)
			{
				return childPosition;
			}

			public override View GetChildView(int groupPosition, int childPosition, bool isLastChild,
				View convertView, ViewGroup parent)
			{
				View view = convertView;
				if (view == null)
				{
					view = LayoutInflater.From(_context).Inflate(Droid.Resource.Layout.SimpleListItem1, parent, false);
				}
				((TextView)view).SetText(((Sample)GetChild(groupPosition, childPosition)).Name, TextView.BufferType.Normal);
				return view;
			}

			public override int GetChildrenCount(int groupPosition)
			{
				return ((SampleGroup)GetGroup(groupPosition)).Samples.Count;
			}

			public override Java.Lang.Object GetGroup(int groupPosition)
			{
				return _sampleGroups.ElementAt(groupPosition);
			}

			public override long GetGroupId(int groupPosition)
			{
				return groupPosition;
			}

			public override View GetGroupView(int groupPosition, bool isExpanded, View convertView, ViewGroup parent)
			{
				View view = convertView;
				if (view == null)
				{
					view = LayoutInflater.From(_context).Inflate(Droid.Resource.Layout.SimpleExpandableListItem1, parent, false);
				}
				((TextView)view).SetText(((SampleGroup)GetGroup(groupPosition)).Title, TextView.BufferType.Normal);
				return view;
			}

			public override int GroupCount => _sampleGroups.Count;

			public override bool HasStableIds => false;

			public override bool IsChildSelectable(int groupPosition, int childPosition)
			{
				return true;
			}
		}

		public class SampleGroup : Java.Lang.Object
		{
			public string Title;
			public List<Sample> Samples;

			public SampleGroup(string title)
			{
				Title = title;
				Samples = new List<Sample>();
			}
		}

		public abstract class Sample : Java.Lang.Object
		{
			public string Name;
			public bool PreferExtensionDecoders;
			public UUID DrmSchemeUuid;
			public string DrmLicenseUrl;
			public string[] DrmKeyRequestProperties;

			protected Sample(string name, UUID drmSchemeUuid, string drmLicenseUrl,
				string[] drmKeyRequestProperties, bool preferExtensionDecoders)
			{
				Name = name;
				DrmSchemeUuid = drmSchemeUuid;
				DrmLicenseUrl = drmLicenseUrl;
				DrmKeyRequestProperties = drmKeyRequestProperties;
				PreferExtensionDecoders = preferExtensionDecoders;
			}

			public virtual Intent BuildIntent(Context context)
			{
				var intent = new Intent(context, typeof(PlayerActivity));
				intent.PutExtra(PlayerActivity.PreferExtensionDecoders, PreferExtensionDecoders);
				if (DrmSchemeUuid != null)
				{
					intent.PutExtra(PlayerActivity.DrmSchemeUuidExtra, DrmSchemeUuid.ToString());
					intent.PutExtra(PlayerActivity.DrmLicenseUrl, DrmLicenseUrl);
					intent.PutExtra(PlayerActivity.DrmKeyRequestProperties, DrmKeyRequestProperties);
				}
				return intent;
			}
		}

		public class UriSample : Sample
		{
			public string Uri;
			public string Extension;

			public UriSample(string name, UUID drmSchemeUuid, string drmLicenseUrl,
				string[] drmKeyRequestProperties, bool preferExtensionDecoders, string uri,
				string extension)
				: base(name, drmSchemeUuid, drmLicenseUrl, drmKeyRequestProperties, preferExtensionDecoders)
			{
				Uri = uri;
				Extension = extension;
			}

			public override Intent BuildIntent(Context context)
			{
				return base.BuildIntent(context)
				   	.SetData(global::Android.Net.Uri.Parse(Uri))
					.PutExtra(PlayerActivity.ExtensionExtra, Extension)
					.SetAction(PlayerActivity.ActionView);
			}

		}

		public class PlaylistSample : Sample
		{
			public UriSample[] Children;

			public PlaylistSample(string name, UUID drmSchemeUuid, string drmLicenseUrl,
				string[] drmKeyRequestProperties, bool preferExtensionDecoders, UriSample[] children)
				: base(name, drmSchemeUuid, drmLicenseUrl, drmKeyRequestProperties, preferExtensionDecoders)
			{
				Children = children;
			}

			public override Intent BuildIntent(Context context)
			{
				string[] urls = new string[Children.Length];
				string[] extensions = new string[Children.Length];
				for (int i = 0; i < Children.Length; i++)
				{
					urls[i] = Children[i].Uri;
					extensions[i] = Children[i].Extension;
				}
				return base.BuildIntent(context)
					.PutExtra(PlayerActivity.UriListExtra, urls)
					.PutExtra(PlayerActivity.ExtensionListExtra, extensions)
					.SetAction(PlayerActivity.ActionViewList);
			}
		}
	}
}
