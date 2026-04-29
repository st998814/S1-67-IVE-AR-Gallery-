using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using SimpleJSON;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using UnityEngine.Video;
using YoutubeLight;

namespace LightShaft.Scripts
{
    [RequireComponent(typeof(YoutubeVideoController))]
    [RequireComponent(typeof(YoutubeVideoEvents))]
    public class YoutubeSimplifiedRequest : MonoBehaviour
    {
        protected string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/94.0.4606.41 Safari/537.36";

        public enum YoutubeVideoQuality
        {
            Standard,
            Hd,
            Fullhd,
            Uhd1440,
            Uhd2160
        }

        public enum VideoFormatType
        {
            Mp4,
            Webm
        }

        public enum Layout3D
        {
            SideBySide,
            OverUnder,
            None,
            EAC,
            EAC3D
        }

        [HideInInspector] public YoutubeVideoController controller;
        [HideInInspector] public YoutubeVideoEvents events;

        [Space] [Tooltip("You can put urls that start at a specific time example: 'https://youtu.be/1G1nCxxQMnA?t=67'")]
        public string youtubeUrl;

        [Space]
        [Space]
        [Tooltip(
            "The desired video quality you want to play. It's in experimental mod, because we need to use 2 video players in qualities 720+, you can expect some desync, but we are working to find a definitive solution to that. Thanks to DASH format.")]
        public YoutubeVideoQuality videoQuality;
        
        [SerializeField]
        [Tooltip("This option force the video output to be mp4 if is available (Some users with Windows11 have issues in the editor )")]
        private bool _forceMP4;

        [Space] [Tooltip("If it is a 360 degree video")]
        public bool is360;

        [Space] [Header("Playback Options")] [Space] [Tooltip("Play the video when the script initialize")]
        public bool autoPlayOnStart = true;

        [Space] [Tooltip("Start playing the video from a desired time")]
        public bool startFromSecond;

        [DrawIf("startFromSecond", true)] public int startFromSecondTime;

        protected bool PrepareVideoToPlayLater;
        [Space] public bool showThumbnailBeforeVideoLoad;

        [DrawIf("showThumbnailBeforeVideoLoad", true)]
        public Renderer thumbnailObject;

        [Space] public bool customPlaylist;
        [DrawIf("customPlaylist", true)] public bool autoPlayNextVideo;

        [Header("If is a custom playlist put urls here")]
        public string[] youtubeUrls;

        protected int CurrentUrlIndex = 0;
        private string youtubeVideoID;


        [Header("You can Try different formats")]
        public VideoFormatType videoFormat;

        [Space]
        [Header("Start Load and Play Url On enable this gameobject")]
        [Tooltip("Play or continue when OnEnable is called")]
        public bool autoPlayOnEnable;

        [Space]
        [Header("Use Device Video player (Standard quality only)")]
        [Tooltip("Play video in mobiles using the mobile device video player not unity internal player")]
        public bool playUsingInternalDevicePlayer;

        [Space]
        [Header("Only load the url to use in a custom player.")]
        [Space]
        [Tooltip(
            "If you want to use your custom player, you can enable this and set the callback OnYoutubeUrlLoaded to your custom function sending the loaded url.")]
        public bool loadYoutubeUrlsOnly;

        [Space]
        [Header("Render the same video to more objects")]
        [Tooltip("Render the same video player material to a different materials, if you want")]
        public GameObject[] objectsToRenderTheVideoImage;

        [Space] [Header("Option for 3D video Only.")] [Tooltip("If the video is a 3D video sidebyside or Over/Under")]
        public bool is3DLayoutVideo;

        [DrawIf("is3DLayoutVideo", true)] public Layout3D layout3d;

        [Space] public Camera mainCamera;

        [Space] [Header("The unity video players")] [Tooltip("The unity video player")]
        public VideoPlayer videoPlayer;

        [Tooltip("The audio player, (Needed for videos that dont have audio included 720p+)")]
        public VideoPlayer audioPlayer;

        [Space] [Tooltip("Show the output in the console")]
        public bool debug;

        [Space] [Tooltip("Ignore timeout is good for very low connections")]
        public bool ignoreTimeout;

        [FormerlySerializedAs("_skipOnDrop")] [Header("If you are having issues with sync try check this and change video format to WEBM")]
        public bool skipOnDrop;


        //Youtube formated urls
        [HideInInspector] public string videoUrl;
        [HideInInspector] public string audioUrl;
        [HideInInspector] public bool progressStartDrag;

        //Request from youtube url timeout
        private readonly int maxRequestTime = 5;

        private float currentRequestTime;

        //When the video fails how much time we will try until try to get from the webserver system.
        private readonly int retryTimeUntilToRequestFromServer = 1;
        private int currentRetryTime;

        //Check when we are trying to get the url
        private bool gettingYoutubeURL;

        //When the urls are done and the video are ready to start playing
        private bool videoAreReadyToPlay;
        protected bool YoutubeUrlReady;

        private string lastTryVideoId;
        private bool videoStarted;
        
        private bool decryptionNeeded;
        private bool startPlaying;

        private float oldVolume;
        private bool waitAudioSeek;
        
        private const string RateBypassFlag = "ratebypass";
        private static string _signatureQuery = "sig";
        private const string Sp = "";
        private static string _projectionType = "";
        private float totalVideoDuration;
        private float currentVideoDuration;
        private bool lowRes;
        private float hideScreenTime;
        private float audioDuration;


        public Material skyboxMaterialNormal;
        public Material skyboxMaterial3DSide;
        private bool loadingFromServer;
        private static string _jsUrlDownloaded;
        private static bool _jsDownloaded;
        
        private static string _jsUrl;
        protected bool FullscreenModeEnabled;

        bool videoEnded;
        [HideInInspector] public string videoTitle = "";
        [FormerlySerializedAs("EACMaterial")] public Material eacMaterial;
        [FormerlySerializedAs("Material360")] public Material material360;

        private List<VideoInfo> _youtubeVideoInfos;
        protected bool FinishedCalled;
        protected bool StartedFromTime;

        //Setup the skybox for 3D or VR videos
        private void Skybox3DSettup()
        {
            if (is3DLayoutVideo)
            {
                if (layout3d == Layout3D.OverUnder)
                {
                    RenderSettings.skybox =
                        (Material)Resources.Load("Materials/PanoramicSkybox3DOverUnder");
                }
                else if (layout3d == Layout3D.SideBySide)
                {
                    RenderSettings.skybox = (Material)Resources.Load("Materials/PanoramicSkybox3Dside");
                }
                else if (layout3d == Layout3D.EAC)
                {
                    RenderSettings.skybox = (Material)Resources.Load("Materials/PanoramicSkyboxEAC");
                }
                else if (layout3d == Layout3D.EAC3D)
                {
                    RenderSettings.skybox = (Material)Resources.Load("Materials/PanoramicSkybox3DEAC");
                }
                else if (layout3d == Layout3D.None)
                {
                    RenderSettings.skybox = (Material)Resources.Load("Materials/PanoramicSkybox3Dside");
                }
            }
        }

        private string tmpv;

        private void YoutubeGetPlayableURL()
        {
            decryptionNeeded = true;
            StartCoroutine(YoutubeGenerateUrlUsingClient());
        }

        private JObject requestResult;
        private bool alreadyGotUrls;


        private static string _visitorData = "";

        IEnumerator GetVisitorData()
        {
            if (!string.IsNullOrWhiteSpace(_visitorData))
                yield return null;

            //"com.google.android.youtube/20.10.38 (Linux; U; ANDROID 11) gzip"
            UnityWebRequest request = UnityWebRequest.Get("https://www.youtube.com/sw.js_data");
            request.SetRequestHeader("User-Agent",
                VRAGENT);
            yield return request.SendWebRequest();
            var jsonString = request.downloadHandler.text;

            if (jsonString.StartsWith(")]}'"))
                jsonString = jsonString[4..];

            var json = JSON.Parse(jsonString);
            var value = json[0][2][0][0][13].Value;

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new Exception("Failed to resolve visitor data.");
            }

            _visitorData = value;
        }

        protected IEnumerator PreventFinishToBeCalledTwoTimes()
        {
            yield return new WaitForSeconds(1);
            FinishedCalled = false;
        }

        private const string VRAGENT = "com.google.android.apps.youtube.vr.oculus/1.60.19 (Linux; U; Android 12L; Quest 3 Build/SQ3A.220605.009.A1) gzip";

        IEnumerator YoutubeGenerateUrlUsingClient()
        {
            alreadyGotUrls = false;

            yield return GetVisitorData();

            CheckVideoUrlAndExtractThevideoId(youtubeUrl);

            string vr = $@"
            {{
            ""videoId"": ""{youtubeVideoID}"",
            ""contentCheckOk"": true,
            ""context"": {{
                ""client"": {{
                ""clientName"": ""ANDROID_VR"",
                ""clientVersion"": ""1.60.19"",
                ""deviceMake"": ""Oculus"",
                ""deviceModel"": ""Quest 3"",
                ""osName"": ""Android"",
                ""osVersion"": ""12L"",
                ""platform"": ""MOBILE"",
                ""visitorData"": ""{_visitorData}"",
                ""hl"": ""en"",
                ""gl"": ""US"",
                ""utcOffsetMinutes"": 0
                }}
            }}
            }}";

            WWWForm form = new WWWForm();
            // string f =
            //     "{\"context\": {\"client\": {\"clientName\": \"IOS\",\"clientVersion\": \"19.29.1\",\"deviceMake\": \"Apple\",\"deviceModel\": \"iPhone16,2\",\"hl\": \"en\",\"osName\": \"iPhone\",\"osVersion\": \"17.5.1.21F90\",\"timeZone\": \"UTC\",\"gl\": \"US\",\"userAgent\": \"com.google.ios.youtube/19.29.1 (iPhone16,2; U; CPU iOS 17_5_1 like Mac OS X;)\"}},\"videoId\": \"" +
            //     youtubeVideoID + "\",\"contentCheckOk\": \"true\",}";
            string android =
                "{\"context\": {\"client\": {\"clientName\": \"ANDROID\",\"clientVersion\": \"20.10.38\",\"osName\": \"Android\",\"osVersion\": \"11\",\"platform\": \"MOBILE\",\"visitorData\":\"" +
                _visitorData +
                "\",\"userAgent\": \"com.google.android.youtube/20.10.38 (Linux; U; ANDROID 11) gzip\", \"hl\": \"en\",\"gl\": \"US\",\"utcOffsetMinutes\": \"0\"}},\"videoId\": \"" +
                youtubeVideoID + "\",}";
            // string fweb =
            //     "{\"context\": {\"client\": {\"clientName\": \"WEB\",\"clientVersion\": \"2.20220801.00.00\"}},\"videoId\": \"" +
            //     youtubeVideoID + "\",}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(vr);
            UnityWebRequest request = UnityWebRequest.Post(
                "https://www.youtube.com/youtubei/v1/player?key=AIzaSyA8eiZmM1FaDVjRy-df2KTyQ_vz_yYM39w", form);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.SetRequestHeader("Content-Type", "application/json");

            string userAgentTemporary = "com.google.android.youtube/20.10.38 (Linux; U; ANDROID 11) gzip";
            request.SetRequestHeader("User-Agent", VRAGENT);
            yield return request.SendWebRequest();
            request.uploadHandler.Dispose();

            JObject audioObject = JObject.Parse(request.downloadHandler.text);
            var formats = audioObject["streamingData"]?["formats"];


            //FallbackSystem only:
            /*
            WWWForm newform = new WWWForm();
            string nf =
                "{\"context\": {\"client\": {\"clientName\": \"IOS\",\"clientVersion\": \"19.45.4\",\"deviceMake\": \"Apple\",\"deviceModel\": \"iPhone16,2\",\"osName\": \"IOS\",\"osVersion\": \"18.1.0.22B83\",\"visitorData\": \"" +
                _visitorData +
                "\",\"platform\": \"MOBILE\",\"hl\": \"en\",\"osName\": \"iPhone\",\"osVersion\": \"17.5.1.21F90\",\"timeZone\": \"UTC\",\"gl\": \"US\",\"userAgent\": \"com.google.ios.youtube/19.29.1 (iPhone16,2; U; CPU iOS 17_5_1 like Mac OS X;)\"}},\"videoId\": \"" +
                youtubeVideoID + "\",\"contentCheckOk\": \"true\",}"; //TODO split the ios section in a const config
            byte[] nbodyRaw = Encoding.UTF8.GetBytes(android);
            UnityWebRequest nrequest = UnityWebRequest.Post(
                "https://www.youtube.com/youtubei/v1/player?key=AIzaSyA8eiZmM1FaDVjRy-df2KTyQ_vz_yYM39w", form);
            nrequest.uploadHandler = new UploadHandlerRaw(nbodyRaw);
            nrequest.SetRequestHeader("Content-Type", "application/json");
            string nuserAgentTemporary =
                "com.google.ios.youtube/19.45.4 (iPhone16,2; U; CPU iOS 18_1_0 like Mac OS X; US)"; //TODO split the ios section in a const config
            nrequest.SetRequestHeader("User-Agent", nuserAgentTemporary);
            yield return nrequest.SendWebRequest();
            nrequest.uploadHandler.Dispose();

            if (nrequest.error != null)
            {
                Debug.Log("Error: " + request.error);
            }
            else
            {*/
            if (decryptionNeeded)
            {
                requestResult = JObject.Parse(request.downloadHandler.text); //User fallback request here.
                var streamingData = requestResult["streamingData"];
                if (streamingData != null) streamingData["formats"] = formats;
                if (debug) Debug.Log("want to write log?");
                IEnumerable<ExtractionInfo> downloadUrls = ExtractDownloadUrls(requestResult);
                _youtubeVideoInfos = GetVideoInfos(downloadUrls, videoTitle).ToList();
                videoTitle = GetVideoTitle(requestResult);
                is360 = false;
                alreadyGotUrls = true;
                UrlsLoaded();
            }

            //if no 360 play fullscreen.
            if (is360)
            {
                if (request.downloadHandler.text.Contains("EQUIRECTANGULAR_THREED_TOP_BOTTOM"))
                {
                    if (debug)
                        Debug.Log("IS 3D");
                    RenderSettings.skybox = skyboxMaterial3DSide;
                    if (!alreadyGotUrls)
                        UrlsLoaded();
                }
                else
                {
                    if (!loadingFromServer)
                    {
                        loadingFromServer = true;
                        if (debug)
                            Debug.Log("Not a 3D");
                        RenderSettings.skybox = skyboxMaterialNormal;
                        //LoadANon3DVideoFromServer(_videoUrl, _formatCode);
                        if (!alreadyGotUrls)
                            UrlsLoaded();
                    }
                }
            }
            else
            {
                if (!alreadyGotUrls)
                {
                    requestResult = JObject.Parse(request.downloadHandler.text);

                    IEnumerable<ExtractionInfo> downloadUrls = ExtractDownloadUrls(requestResult);
                    _youtubeVideoInfos = GetVideoInfos(downloadUrls, videoTitle).ToList();
                    request.downloadHandler.Dispose();
                    if (!alreadyGotUrls)
                        UrlsLoaded(); //call direct to extract the video infos.
                }
            }
            //}
        }

        private void FixCameraEvent()
        {
            if (mainCamera == null)
            {
                if (Camera.main != null)
                    mainCamera = Camera.main;
                else
                {
                    mainCamera = GameObject.FindObjectOfType<Camera>();
                    Debug.Log("Add the main camera to the mainCamera field");
                }
            }

            if (videoPlayer.renderMode == VideoRenderMode.CameraFarPlane ||
                videoPlayer.renderMode == VideoRenderMode.CameraNearPlane)
                videoPlayer.targetCamera = mainCamera;
        }

        private string CheckVideoUrlAndExtractThevideoId(string url)
        {
            if (url.Contains("?t="))
            {
                int last = url.LastIndexOf("?t=", StringComparison.Ordinal);
                string copy = url;
                string newString = copy.Remove(0, last);
                newString = newString.Replace("?t=", "");
                startFromSecond = true;
                startFromSecondTime = int.Parse(newString);
                url = url.Remove(last);
            }

            bool isYoutubeUrl = TryNormalizeYoutubeUrlLocal(url, out url);
            if (!isYoutubeUrl)
            {
                url = "none";
                OnYoutubeError("Not a Youtube Url");
            }

            return url;
        }

        private void OnYoutubeError(string errorType)
        {
            Debug.Log("<color=red>" + errorType + "</color>");
        }

        private bool TryNormalizeYoutubeUrlLocal(string url, out string normalizedUrl)
        {
            url = url.Trim();
            url = url.Replace("youtu.be/", "youtube.com/watch?v=");
            url = url.Replace("www.youtube", "youtube");
            url = url.Replace("youtube.com/embed/", "youtube.com/watch?v=");

            if (url.Contains("/v/"))
            {
                url = "https://youtube.com" + new Uri(url).AbsolutePath.Replace("/v/", "/watch?v=");
            }

            url = url.Replace("/watch#", "/watch?");
            IDictionary<string, string> query = HTTPHelperYoutube.ParseQueryString(url);
            
            if (!query.TryGetValue("v", out var v))
            {
                normalizedUrl = null;
                return false;
            }

            youtubeVideoID = v;
            normalizedUrl = "https://youtube.com/watch?v=" + v;
            return true;
        }


        private void Awake()
        {
            skipOnDrop = true;

            if (!loadYoutubeUrlsOnly)
            {
                if (GetComponent<YoutubeVideoController>() == null)
                {
                    Debug.LogError("You need a VideoController attached to YoutubePlayer");
                    return;
                }

                if (GetComponent<YoutubeVideoEvents>() == null)
                {
                    Debug.LogError("You need a VidepoEvents attached to YoutubePlayer");
                    return;
                }

                controller = GetComponent<YoutubeVideoController>();
                events = GetComponent<YoutubeVideoEvents>();

                //Check if fullscreen Mode is active at start.
                if (videoPlayer.renderMode == VideoRenderMode.CameraFarPlane ||
                    videoPlayer.renderMode == VideoRenderMode.CameraNearPlane)
                    FullscreenModeEnabled = true;
                else FullscreenModeEnabled = false;
            }

            if (loadYoutubeUrlsOnly) //TODO Temp fix for play with external player.
            {
                controller = GetComponent<YoutubeVideoController>();
                events = GetComponent<YoutubeVideoEvents>();
            }

            if (!playUsingInternalDevicePlayer && !loadYoutubeUrlsOnly)
            {
                if (is360)
                {
                    if (videoQuality == YoutubeVideoQuality.Standard)
                        videoQuality = YoutubeVideoQuality.Hd; //Does not play Standard for 360 degree videos.
                }

                if (videoQuality == YoutubeVideoQuality.Standard) //Disable the second video player to eco resource;
                {
                    if (videoFormat == VideoFormatType.Webm) videoPlayer.skipOnDrop = skipOnDrop;
                    if (audioPlayer != null)
                        audioPlayer.transform.gameObject.SetActive(false);
                }
            }
        }

        long lastFrame = -1;

        void VerifyFrames()
        {
            if (!playUsingInternalDevicePlayer)
            {
                if (videoPlayer.isPlaying)
                {
                    if (lastFrame == videoPlayer.frame)
                    {
                        audioPlayer.Pause();
                        videoPlayer.Pause();
                        StartCoroutine(WaitSync());
                    }

                    lastFrame = videoPlayer.frame;
                    Invoke(nameof(VerifyFrames), 2);
                }
            }
        }

        public virtual void Start()
        {
            if (videoPlayer != null)
            {
                if (videoPlayer.targetTexture != null)
                {
                    switch (videoQuality)
                    {
                        case YoutubeVideoQuality.Standard:
                            videoPlayer.targetTexture.width = 640;
                            videoPlayer.targetTexture.height = 360;
                            break;
                        case YoutubeVideoQuality.Hd:
                            videoPlayer.targetTexture.width = 1280;
                            videoPlayer.targetTexture.height = 720;
                            break;
                        case YoutubeVideoQuality.Fullhd:
                            videoPlayer.targetTexture.width = 1920;
                            videoPlayer.targetTexture.height = 1080;
                            break;
                        case YoutubeVideoQuality.Uhd1440:
                            videoPlayer.targetTexture.width = 2560;
                            videoPlayer.targetTexture.height = 1440;
                            break;
                        case YoutubeVideoQuality.Uhd2160:
                            videoPlayer.targetTexture.width = 3840;
                            videoPlayer.targetTexture.height = 2160;
                            break;
                    }
                }
            }


            if (playUsingInternalDevicePlayer)
                loadYoutubeUrlsOnly = true;
#if UNITY_WEBGL
        videoQuality = YoutubeVideoQuality.Hd;
#endif
#if UNITY_STANDALONE_WIN
            //Force play webm on windows builds to prevent a issue on early windows 11 codecs.
            if (_forceMP4 is false)
                videoFormat = VideoFormatType.Webm;
            else
                videoFormat = VideoFormatType.Mp4;
#endif
            if (!loadYoutubeUrlsOnly)
            {
                Invoke(nameof(VerifyFrames), 2);
                FixCameraEvent();
                Skybox3DSettup();

                //I used this in version 5.1 but some users don't like, you may enable if you want to test, this prevent the video to be out of sync sometimes, but there's a lot of lag in playback
                if (videoFormat == VideoFormatType.Webm)
                {
                    videoPlayer.skipOnDrop = skipOnDrop;
                    if (audioPlayer != null)
                        audioPlayer.skipOnDrop = skipOnDrop;
                }

                audioPlayer.seekCompleted += AudioSeeked;
                videoPlayer.seekCompleted += VideoSeeked;
            }

            PrepareVideoPlayerCallbacks();

            if (autoPlayOnStart)
            {
                PlayYoutubeVideo(customPlaylist ? youtubeUrls[CurrentUrlIndex] : youtubeUrl);
            }

            lowRes = videoQuality == YoutubeVideoQuality.Standard;
        }

        protected void DisableThumbnailObject()
        {
            if (thumbnailObject != null)
                thumbnailObject.gameObject.SetActive(false);
        }

        private void EnableThumbnailObject()
        {
            if (thumbnailObject != null)
                thumbnailObject.gameObject.SetActive(true);
            else Debug.Log("Thumbnail object is null");
        }

        private void TryToLoadThumbnailBeforeOpenVideo(string id)
        {
            string tempId = id.Replace("https://youtube.com/watch?v=", "");
            StartCoroutine(DownloadThumbnail(tempId));
        }

        IEnumerator DownloadThumbnail(string videoId)
        {
            UnityWebRequest request =
                UnityWebRequestTexture.GetTexture("https://img.youtube.com/vi/" + videoId + "/0.jpg");
            yield return request.SendWebRequest();
            EnableThumbnailObject();
            Texture2D thumb = DownloadHandlerTexture.GetContent(request);
            thumbnailObject.material.mainTexture = thumb;
        }

        private void FixedUpdate()
        {
            if (videoPlayer)
            {
                if (videoPlayer.isPlaying)
                {
                    if (!lowRes)
                    {
                        if (controller.volumeSlider)
                        {
                            if (videoPlayer.GetTargetAudioSource(0).volume <= 0)
                                videoPlayer.GetTargetAudioSource(0).volume = controller.volumeSlider.value;
                        }
                        else
                        {
                            videoPlayer.GetTargetAudioSource(0).volume = 1;
                        }
                    }
                    else
                    {
                        if (audioPlayer)
                        {
                            if (controller.volumeSlider)
                            {
                                if (audioPlayer.GetTargetAudioSource(0).volume <= 0)
                                    audioPlayer.GetTargetAudioSource(0).volume = controller.volumeSlider.value;
                            }
                            else
                            {
                                audioPlayer.GetTargetAudioSource(0).volume = 1;
                            }
                        }
                    }
                }
            }

            if (!loadYoutubeUrlsOnly)
            {
                if (!playUsingInternalDevicePlayer)
                {
                    if (videoPlayer.isPlaying)
                        HideLoading();
                    else
                    {
                        if (!pauseCalled && !PrepareVideoToPlayLater)
                            ShowLoading();
                    }
                }
            }

            if (!loadYoutubeUrlsOnly)
            {
                if (controller.showPlayerControl)
                {
                    if (videoPlayer.isPlaying)
                    {
                        totalVideoDuration = Mathf.RoundToInt(videoPlayer.frameCount / videoPlayer.frameRate);
                        if (!lowRes)
                        {
                            audioDuration = Mathf.RoundToInt(audioPlayer.frameCount / audioPlayer.frameRate);
                            if (audioDuration < totalVideoDuration && (audioPlayer.url != ""))
                            {
                                currentVideoDuration = Mathf.RoundToInt(audioPlayer.frame / audioPlayer.frameRate);
                            }
                            else
                            {
                                currentVideoDuration = Mathf.RoundToInt(videoPlayer.frame / videoPlayer.frameRate);
                            }
                        }
                        else
                        {
                            currentVideoDuration = Mathf.RoundToInt(videoPlayer.frame / videoPlayer.frameRate);
                        }
                    }
                }

                if (videoPlayer.frameCount > 0)
                {
                    if (controller && controller.showPlayerControl)
                    {
                        if (controller.useSliderToProgressVideo) //use slider
                        {
                            if (!progressStartDrag)
                                controller.playbackSlider.value = (float)videoPlayer.time;
                        }
                        else //use rectangle sprite.
                        {
                            if (controller.progressRectangle)
                                controller.progressRectangle.fillAmount =
                                    videoPlayer.frame / (float)videoPlayer.frameCount;
                        }
                    }
                }
            }

            if (gettingYoutubeURL)
            {
                currentRequestTime += Time.deltaTime;
                if (currentRequestTime >= maxRequestTime)
                {
                    if (!ignoreTimeout)
                    {
                        gettingYoutubeURL = false;
                        if (debug)
                            Debug.Log("<color=blue>Max time reached, trying again!</color>");

                        RetryPlayYoutubeVideo();
                    }
                }
            }

            if (videoAreReadyToPlay)
            {
                videoAreReadyToPlay = false;
            }

            if (!loadYoutubeUrlsOnly)
            {
                if (controller.showPlayerControl)
                {
                    lowRes = videoQuality == YoutubeVideoQuality.Standard;

                    if (controller.currentTime && controller.totalTime)
                    {
                        controller.currentTime.text = FormatTime(Mathf.RoundToInt(currentVideoDuration));
                        if (!lowRes)
                        {
                            if (audioDuration < totalVideoDuration && (audioPlayer.url != ""))
                                controller.totalTime.text = FormatTime(Mathf.RoundToInt(audioDuration));
                            else
                                controller.totalTime.text = FormatTime(Mathf.RoundToInt(totalVideoDuration));
                        }
                        else
                        {
                            controller.totalTime.text = FormatTime(Mathf.RoundToInt(totalVideoDuration));
                        }
                    }
                }

                if (!controller.showPlayerControl)
                {
                    if (controller.controllerMainUI)
                        controller.controllerMainUI.SetActive(false);
                }
                else
                    controller.controllerMainUI.SetActive(true);
            }

            if (!loadYoutubeUrlsOnly)
            {
                if (videoPlayer.isPrepared && !videoPlayer.isPlaying)
                {
                    if (audioPlayer)
                    {
                        if (audioPlayer.isPrepared)
                        {
                            if (!videoStarted)
                            {
                                videoStarted = true;
                                VideoStarted();
                            }
                        }
                    }
                    else
                    {
                        if (!videoStarted)
                        {
                            videoStarted = true;
                            VideoStarted();
                        }
                    }
                }
            }

            if (loadYoutubeUrlsOnly) return;
            if (videoPlayer.frame != 0 && !videoEnded && videoPlayer.isPlaying)
            {
                if (videoPlayer.frame >= (long)videoPlayer.frameCount - 1)
                {
                    videoEnded = true;
                    PlaybackDone();
                }
            }

            if (!videoPlayer.isPrepared || startPlaying) return;
            if (videoQuality != YoutubeVideoQuality.Standard)
            {
                if (audioPlayer.isPrepared)
                {
                    StartPlayingWebgl();
                }
            }
            else
            {
                StartPlayingWebgl();
            }
        }

        private void PrepareVideoPlayerCallbacks()
        {
            videoPlayer.errorReceived += VideoErrorReceived;
            if (videoQuality != YoutubeVideoQuality.Standard)
                audioPlayer.errorReceived += VideoErrorReceived;
        }

        private void ShowLoading()
        {
            if (controller.loading != null)
                controller.loading.SetActive(true);
        }

        private void HideLoading()
        {
            if (controller.loading != null)
                controller.loading.SetActive(false);
        }


        private void ResetThings()
        {
            startPlaying = false;
            gettingYoutubeURL = false;
            progressStartDrag = false;
            videoAreReadyToPlay = false;
            YoutubeUrlReady = false;

            if (audioPlayer != null)
                audioPlayer.seekCompleted += AudioSeeked;
            videoPlayer.seekCompleted += VideoSeeked;

            waitAudioSeek = false;
        }

        protected void PlayYoutubeVideo(string videoId)
        {
            lowRes = videoQuality == YoutubeVideoQuality.Standard;
            ResetThings();
            videoId = CheckVideoUrlAndExtractThevideoId(videoId);
            
            if (videoId != "none")
            {
                //Thumbnail
                if (showThumbnailBeforeVideoLoad)
                    TryToLoadThumbnailBeforeOpenVideo(videoId);
                YoutubeUrlReady = false;
                ShowLoading();
                youtubeUrl = videoId;
                lastTryVideoId = videoId;
                currentRequestTime = 0;
                gettingYoutubeURL = true;
                YoutubeGetPlayableURL();
            }
        }

        //The callback when the url's are loaded.
        private void UrlsLoaded()
        {
            gettingYoutubeURL = false;
            List<VideoInfo> videoInfos = _youtubeVideoInfos;
            if (is360)
            {
                if (videoQuality == YoutubeVideoQuality.Uhd1440 || videoQuality == YoutubeVideoQuality.Uhd2160)
                    videoFormat = VideoFormatType.Webm;
            }

            bool needDecryption = false;

            if (videoQuality != YoutubeVideoQuality.Standard)
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            //Get the video with audio first
            videoInfos.Reverse();

            foreach (var info in videoInfos.Where(info => info.FormatCode == 18))
            {
                StartCoroutine(WaitJsDownload());
                if (info.RequiresDecryption)
                {
                    needDecryption = true;
                }
                else
                {
                    audioUrl = info.DownloadUrl;
                }

                break;
            }

            //Then we will get the desired video quality.
            int quality = 360;
            switch (videoQuality)
            {
                case YoutubeVideoQuality.Standard:
                    quality = 360;
                    break;
                case YoutubeVideoQuality.Hd:
                    quality = 720;
                    break;
                case YoutubeVideoQuality.Fullhd:
                    quality = 1080;
                    break;
                case YoutubeVideoQuality.Uhd1440:
                    quality = 1440;
                    break;
                case YoutubeVideoQuality.Uhd2160:
                    quality = 2160;
                    break;
            }


            bool foundVideo = false;
            videoInfos.Reverse();

            //Get the high quality video
            foreach (VideoInfo info in videoInfos)
            {
                VideoType t = (videoFormat == VideoFormatType.Mp4) ? VideoType.Mp4 : VideoType.WebM;
                if (info.VideoType == t && info.Resolution == (quality))
                {
                    if (debug) Debug.Log(quality);
                    if (is360)
                    {
                        if (!string.IsNullOrEmpty(_projectionType) &&
                            (videoPlayer.renderMode != VideoRenderMode.RenderTexture))
                        {
                            if (_projectionType == "MESH")
                            {
                                //enable equirectangular shader.
                                foreach (var renderMaterial in videoPlayer.targetMaterialRenderer.materials)
                                {
                                    if (renderMaterial.name == "SphereEAC")
                                    {
                                        videoPlayer.targetMaterialRenderer.material = eacMaterial;
                                        videoPlayer.targetMaterialRenderer.gameObject.transform.localScale =
                                            new Vector3(344.9097f, 344.9097f, -344.9097f);
                                        videoPlayer.targetMaterialRenderer.gameObject.transform.localRotation =
                                            Quaternion.Euler(90, -10, -280);
                                    }
                                }
                            }
                            else if (_projectionType == "EQUIRECTANGULAR" || _projectionType == "RECTANGULAR")
                            {
                                //disable equirectangular shader.
                                videoPlayer.targetMaterialRenderer.material = material360;
                                videoPlayer.targetMaterialRenderer.gameObject.transform.localScale =
                                    new Vector3(344.9097f, 344.9097f, 344.9097f);
                                videoPlayer.targetMaterialRenderer.gameObject.transform.localRotation =
                                    Quaternion.Euler(0, 90, -90);
                            }
                            else
                            {
                                Debug.Log("Untested projection type, report to support email.");
                            }
                        }

                        bool found360 = false;
                        switch (info.Resolution)
                        {
                            case 720:
                                if (t == VideoType.Mp4)
                                {
                                    if (info.FormatCode == 136 || info.FormatCode == 298)
                                    {
                                        found360 = true;
                                    }
                                }
                                else
                                {
                                    if (info.FormatCode == 247 || info.FormatCode == 302)
                                    {
                                        found360 = true;
                                    }
                                }

                                break;
                            case 1080:
                                if (t == VideoType.Mp4)
                                {
                                    if (info.FormatCode == 137 || info.FormatCode == 299)
                                    {
                                        found360 = true;
                                    }
                                }
                                else
                                {
                                    if (info.FormatCode == 248 || info.FormatCode == 303)
                                    {
                                        found360 = true;
                                    }
                                }

                                break;
                            case 1440:
                                if (t == VideoType.Mp4)
                                {
                                    if (info.FormatCode == 264)
                                    {
                                        found360 = true;
                                    }
                                }
                                else
                                {
                                    if (info.FormatCode == 271 || info.FormatCode == 308)
                                    {
                                        found360 = true;
                                    }
                                }

                                break;
                            case 2160:
                                if (t == VideoType.Mp4)
                                {
                                    if (info.FormatCode == 266)
                                    {
                                        found360 = true;
                                    }
                                }
                                else
                                {
                                    if (info.FormatCode == 313 || info.FormatCode == 315)
                                    {
                                        found360 = true;
                                    }
                                }

                                break;
                        }

                        if (found360)
                        {
                            if (debug) Debug.Log(info.FormatCode);
                            if (info.RequiresDecryption)
                            {
                                if (debug)
                                    Debug.Log("REQUIRE DECRYPTION!");
                                needDecryption = true;
                            }
                            else
                            {
                                if (debug) Debug.Log(info.DownloadUrl);
                            }

                            foundVideo = true;
                            break;
                        }
                    }
                    else
                    {
                        if (info.Resolution == quality)
                        {
                            videoUrl = info.DownloadUrl;
                            OnYoutubeUrlsLoaded();
                        }

                        foundVideo = true;
                        break;
                    }
                }
            }

            //TRY TO GET WEBM
            if (!foundVideo && quality == 1440)
            {
                foreach (VideoInfo info in videoInfos)
                {
                    if (info.FormatCode == 271)
                    {
                        if (info.RequiresDecryption)
                        {
                            needDecryption = true;
                        }
                        else
                        {
                            videoUrl = info.DownloadUrl;
                            videoAreReadyToPlay = true;
                            OnYoutubeUrlsLoaded();
                        }

                        foundVideo = true;
                        break;
                    }
                }
            }


            if (!foundVideo && quality == 2160)
            {
                foreach (VideoInfo info in videoInfos)
                {
                    if (info.FormatCode == 313)
                    {
                        if (debug)
                            Debug.Log(
                                "Found but with unknow format in results, check to see if the video works normal.");
                        if (info.RequiresDecryption)
                        {
                            needDecryption = true;
                        }
                        else
                        {
                            videoUrl = info.DownloadUrl;
                            videoAreReadyToPlay = true;
                            OnYoutubeUrlsLoaded();
                        }

                        foundVideo = true;
                        break;
                    }
                }
            }

            //if desired quality not found try another lower quality.
            if (!foundVideo)
            {
                if (debug)
                    Debug.Log("Desired quality not found, playing with low quality, check if the video id: " +
                              youtubeUrl + " support that quality!");
                bool found1080 = false;
                VideoType tp = (videoFormat == VideoFormatType.Mp4 ? VideoType.Mp4 : VideoType.WebM);

                foreach (VideoInfo info in videoInfos)
                {
                    if (debug)
                        Debug.Log("RES: " + info.Resolution + " | " + info.FormatCode + " | " + info.VideoType);
                }

                if (tp == VideoType.WebM)
                {
                    tp = VideoType.Mp4;
                }

                foreach (VideoInfo info in videoInfos)
                {
                    if (info.VideoType == tp && info.Resolution == (1080))
                    {
                        found1080 = true;
                        if (info.RequiresDecryption)
                        {
                            videoQuality = YoutubeVideoQuality.Standard;
                            needDecryption = true;
                        }
                        else
                        {
                            videoUrl = info.DownloadUrl;
                        }

                        break;
                    }
                }

                if (found1080 == false)
                {
                    foreach (VideoInfo info in videoInfos)
                    {
                        if (info.VideoType == tp && info.Resolution == (360))
                        {
                            if (info.RequiresDecryption)
                            {
                                videoQuality = YoutubeVideoQuality.Standard;
                                needDecryption = true;
                            }
                            else
                            {
                                videoUrl = info.DownloadUrl;
                            }

                            break;
                        }
                    }
                }
            }
            if (needDecryption)
            {
                decryptionNeeded = true;
            }
        }

        private void StartPlayingWebgl()
        {
            if (startPlaying) return;
            startPlaying = true;
            events.OnVideoReadyToStart.Invoke();

            if (playUsingInternalDevicePlayer && Application.isMobilePlatform) //Works in mobiles only!!
            {
                //Play using the internal player of the device 
                StartCoroutine(HandHeldPlayback());
            }
            else
            {
                StartPlayback();
            }
        }

        IEnumerator HandHeldPlayback()
        {
#if UNITY_ANDROID || UNITY_IOS
        if (videoQuality == YoutubeVideoQuality.STANDARD)
            Handheld.PlayFullScreenMovie(videoUrl, Color.black, FullScreenMovieControlMode.Minimal, FullScreenMovieScalingMode.AspectFit); //Use only the video with audio integrated. Working to get a url with high quality and audio included.
        else
            Handheld.PlayFullScreenMovie(audioUrl, Color.black, FullScreenMovieControlMode.Minimal, FullScreenMovieScalingMode.AspectFit); //Use only the video with audio integrated. Working to get a url with high quality and audio included.
#else
            Debug.Log("This runs in mobile devices only!");
#endif
            yield return new WaitForSeconds(1f);
            PlaybackDone();
        }

        [Header("If your unity version audio desyncs try to play with .4f or other value.")]
        public float audioDelayOffset;

        protected IEnumerator DelayPlay()
        {
            yield return new WaitForSeconds(audioDelayOffset);
            audioPlayer.Play();
        }

        private void StartPlayback()
        {
            //Render to more materials
            if (objectsToRenderTheVideoImage.Length > 0)
            {
                foreach (GameObject obj in objectsToRenderTheVideoImage)
                {
                    obj.GetComponent<Renderer>().material.mainTexture = videoPlayer.texture;
                }
            }

            videoEnded = false;
            events.OnVideoStarted.Invoke();
            HideLoading();
            waitAudioSeek = true;
            if (is360 || videoPlayer.targetTexture != null)
            {
                if (videoPlayer.renderMode == VideoRenderMode.RenderTexture)
                {
                    videoPlayer.targetTexture.width = (int)videoPlayer.width;
                    videoPlayer.targetTexture.height = (int)videoPlayer.height;
                }
            }

            if (videoQuality != YoutubeVideoQuality.Standard)
            {
                audioPlayer.Pause();
                videoPlayer.Pause();
                audioPlayer.time = 1;
                videoPlayer.time = 0;
            }

            if (!PrepareVideoToPlayLater)
            {
                DisableThumbnailObject();
            }

            if (!PrepareVideoToPlayLater)
            {
                events.OnVideoStarted.Invoke();
                if (videoQuality != YoutubeVideoQuality.Standard)
                {
                    //audioPlayer.Play();
                    videoPlayer.Play();

                    StartCoroutine(DelayPlay());
                }
                else
                {
                    videoPlayer.Play();
                }
            }

            if (startFromSecond)
            {
                StartedFromTime = true;
                if (videoQuality == YoutubeVideoQuality.Standard)
                {
                    //seekUsingLowQuality = true;
                    videoPlayer.time = startFromSecondTime;
                }
                else
                {
                    audioPlayer.time = startFromSecondTime;
                }
            }
        }

        /// <summary>
        /// Returns the max video quality supported by the device
        /// </summary>
        /// <returns>The max res supported</returns>
        public int GetMaxQualitySupportedByDevice()
        {
            if (Screen.orientation == ScreenOrientation.LandscapeLeft)
            {
                //use the height
                return Screen.currentResolution.height;
            }
            else if (Screen.orientation == ScreenOrientation.Portrait)
            {
                //use the width
                return Screen.currentResolution.width;
            }
            else
            {
                return Screen.currentResolution.height;
            }
        }

        private void RetryPlayYoutubeVideo()
        {
            Stop();
            currentRetryTime++;
            if (currentRetryTime < retryTimeUntilToRequestFromServer)
            {
                StopIfPlaying();
                if (debug)
                    Debug.Log("Youtube Retrying...:" + lastTryVideoId);
                ShowLoading();
                youtubeUrl = lastTryVideoId;
                PlayYoutubeVideo(youtubeUrl);
            }
            else
            {
                currentRetryTime = 0;
                StopIfPlaying();
                if (debug)
                    Debug.Log("Youtube Retrying...:" + lastTryVideoId);
                ShowLoading();
                youtubeUrl = lastTryVideoId;
                PlayYoutubeVideo(youtubeUrl);
            }
        }

        private void StopIfPlaying()
        {
            if (!loadYoutubeUrlsOnly)
            {
                if (debug)
                    Debug.Log("Stopping video");
                if (videoPlayer.isPlaying)
                {
                    videoPlayer.Stop();
                }

                if (audioPlayer != null)
                    if (audioPlayer.isPlaying)
                    {
                        audioPlayer.Stop();
                    }
            }
        }

        private void OnYoutubeUrlsLoaded()
        {
            YoutubeUrlReady = true;
            if (videoQuality == YoutubeVideoQuality.Standard)
            {
                videoUrl = audioUrl;
            }

            if (loadYoutubeUrlsOnly)
            {
                Debug.Log("Url Generated to play, you can use the event callback: " + videoUrl);
                if (events != null)
                    events.OnYoutubeUrlAreReady.Invoke(videoUrl);
            }

            if (!loadYoutubeUrlsOnly) //If want to load urls only the video will not play
            {
                if (debug)
                    Debug.Log("Url Generated to play!!" + videoUrl);

                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = videoUrl;
                videoPlayer.EnableAudioTrack(0, true);
                videoPlayer.SetTargetAudioSource(0, videoPlayer.GetComponent<AudioSource>());
                videoPlayer.Prepare();
                if (videoQuality != YoutubeVideoQuality.Standard)
                {
                    audioPlayer.source = VideoSource.Url;
                    audioPlayer.url = audioUrl;
                    audioPlayer.Prepare();
                }
            }
            else
            {
                if (playUsingInternalDevicePlayer)
                {
                    StartCoroutine(HandHeldPlayback());
                }
            }
        }

        private void PlaybackDone()
        {
            videoStarted = false;
            events.OnVideoFinished.Invoke();
        }
        
        private void VideoStarted()
        {
            if (!videoStarted)
            {
                if (debug)
                    Debug.Log("Youtube Video Started");
            }
        }

        private void VideoErrorReceived(VideoPlayer source, string message)
        {
            Debug.Log($"Video Error: {message}");
        }

        [HideInInspector] public bool pauseCalled;

        public void Pause()
        {
            Debug.Log("PAUSE");
            pauseCalled = true;
            if (videoQuality == YoutubeVideoQuality.Standard)
            {
                videoPlayer.Pause();
            }
            else
            {
                audioPlayer.GetTargetAudioSource(0).volume = 0;
                videoPlayer.Pause();
                audioPlayer.Pause();
                audioPlayer.time = videoPlayer.time;
            }

            events.OnVideoPaused.Invoke();
        }

        private void Update()
        {
            if (loadYoutubeUrlsOnly) return;
            if (!controller.showPlayerControl) return;
            if (controller.hideScreenControlTime <= 0) return;

            if (UserInteract())
            {
                hideScreenTime = 0;
                if (controller.controllerMainUI)
                    controller.controllerMainUI.SetActive(true);
            }
            else
            {
                hideScreenTime += Time.deltaTime;
                if (!(hideScreenTime >= controller.hideScreenControlTime)) return;
                hideScreenTime = controller.hideScreenControlTime;
                controller.HideControllers();
            }
        }

        protected void Stop()
        {
            PrepareVideoToPlayLater = false;
            if (!playUsingInternalDevicePlayer)
            {
                if (audioPlayer != null)
                    audioPlayer.seekCompleted -= AudioSeeked;
                videoPlayer.seekCompleted -= VideoSeeked;

                videoPlayer.Stop();
                if (!lowRes && audioPlayer != null)
                    audioPlayer.Stop();
            }
        }

        private static string FormatTime(int time)
        {
            int hours = time / 3600;
            int minutes = (time % 3600) / 60;
            int seconds = (time % 3600) % 60;
            if (hours == 0 && minutes != 0)
            {
                return minutes.ToString("00") + ":" + seconds.ToString("00");
            }
            else if (hours == 0 && minutes == 0)
            {
                return "00:" + seconds.ToString("00");
            }
            else
            {
                return hours.ToString("00") + ":" + minutes.ToString("00") + ":" + seconds.ToString("00");
            }
        }

        bool UserInteract()
        {
            if (Application.isMobilePlatform)
            {
                if (Input.touches.Length >= 1)
                    return true;
                else
                    return false;
            }
            else
            {
                if (Input.GetMouseButtonDown(0))
                    return true;
                return (Input.GetAxis("Mouse X") != 0) || (Input.GetAxis("Mouse Y") != 0);
            }
        }

        private static IEnumerable<ExtractionInfo> ExtractDownloadUrls(JObject json)
        {
            List<string> urls = new List<string>();
            List<string> ciphers = new List<string>();
            JObject newJson = json;
            
            var streammingData = newJson["streamingData"];
            var adaptiveFormats = streammingData["adaptiveFormats"];
            
            if (newJson["streamingData"]["formats"] != null)
            {
                if (newJson["streamingData"]["formats"][0]?["cipher"] != null)
                {
                    foreach (var j in newJson["streamingData"]["formats"])
                    {
                        ciphers.Add(j["cipher"]?.ToString());
                    }

                    if (adaptiveFormats != null)
                    {
                        foreach (var j in adaptiveFormats)
                        {
                            ciphers.Add(j["cipher"]?.ToString());
                        } 
                    }
                }
                else if (newJson["streamingData"]["formats"][0]?["signatureCipher"] != null)
                {
                    foreach (var j in newJson["streamingData"]["formats"])
                    {
                        ciphers.Add(j["signatureCipher"]?.ToString());
                    }

                    if (adaptiveFormats != null)
                    {
                        foreach (var j in adaptiveFormats)
                        {
                            ciphers.Add(j["signatureCipher"]?.ToString());
                        }
                    }
                }
                else
                {
                    //WriteLog("test", newJson.ToString());
                    if (newJson["streamingData"]["formats"] != null)
                    {
                        foreach (var j in newJson["streamingData"]["formats"])
                        {
                            urls.Add(j["url"]?.ToString());
                        }
                    }

                    if (adaptiveFormats != null)
                    {
                        foreach (var j in adaptiveFormats)
                        {
                            if (j["itag"]?.ToString() == "134")
                            {
                                if (j["projectionType"] != null)
                                    _projectionType = j["projectionType"].ToString();
                            }

                            urls.Add(j["url"]?.ToString());
                        }
                    }
                }
            }
            else
            {
                if (newJson["streamingData"]["formats"] != null)
                {
                    foreach (var j in newJson["streamingData"]["formats"])
                    {
                        urls.Add(j["url"]?.ToString());
                    }
                }

                if (adaptiveFormats != null)
                {
                    foreach (var j in adaptiveFormats)
                    {
                        if (j["itag"]?.ToString() == "134")
                        {
                            if (j["projectionType"] != null)
                                _projectionType = j["projectionType"].ToString();
                        }
                        urls.Add(j["url"]?.ToString());
                    }
                }
            }

            foreach (string s in ciphers)
            {
                IDictionary<string, string> queries = HTTPHelperYoutube.ParseQueryString(s);
                string url;
                bool requiresDecryption = false;
                
                _signatureQuery = queries.ContainsKey("sp") ? "sig" : "signatures";

                if (queries.ContainsKey("s") || queries.ContainsKey("signature"))
                {
                    requiresDecryption = queries.ContainsKey("s");
                    string signature = queries.TryGetValue("s", out var query) ? query : queries["signature"];

                    if (Sp != "none")
                    {
                        url = $"{queries["url"]}&{_signatureQuery}={signature}";
                    }
                    // else
                    // {
                    //     url = string.Format("{0}&{1}={2}", queries["url"], _signatureQuery, signature);
                    // }

                    string fallbackHost = queries.TryGetValue("fallback_host", out var query1)
                        ? "&fallback_host=" + query1
                        : String.Empty;
                    url += fallbackHost;
                }
                else
                {
                    url = queries["url"];
                }
                url = HTTPHelperYoutube.UrlDecode(url);
                IDictionary<string, string> parameters = HTTPHelperYoutube.ParseQueryString(url);
                if (!parameters.ContainsKey(RateBypassFlag))
                    url += $"&{RateBypassFlag}=yes";
                yield return new ExtractionInfo { RequiresDecryption = requiresDecryption, Uri = new Uri(url) };
            }

            foreach (string s in urls)
            {
                string url = s;
                url = HTTPHelperYoutube.UrlDecode(url);
                url = HTTPHelperYoutube.UrlDecode(url);

                IDictionary<string, string> parameters = HTTPHelperYoutube.ParseQueryString(url);
                if (!parameters.ContainsKey(RateBypassFlag))
                    url += $"&{RateBypassFlag}=yes";
                yield return new ExtractionInfo { RequiresDecryption = false, Uri = new Uri(url) };
            }
        }

        private static IEnumerable<VideoInfo> GetVideoInfos(IEnumerable<ExtractionInfo> extractionInfos,
            string videoTitle)
        {
            var downLoadInfos = new List<VideoInfo>();

            foreach (ExtractionInfo extractionInfo in extractionInfos)
            {
                string itag = HTTPHelperYoutube.ParseQueryString(extractionInfo.Uri.Query)["itag"];
                int formatCode = int.Parse(itag);

                VideoInfo info = VideoInfo.Defaults.SingleOrDefault(videoInfo => videoInfo.FormatCode == formatCode);

                if (info != null)
                {
                    info = new VideoInfo(info)
                    {
                        DownloadUrl = extractionInfo.Uri.ToString(),
                        Title = videoTitle,
                        RequiresDecryption = extractionInfo.RequiresDecryption
                    };
                }
                else
                {
                    info = new VideoInfo(formatCode)
                    {
                        DownloadUrl = extractionInfo.Uri.ToString()
                    };
                }
                downLoadInfos.Add(info);
            }
            return downLoadInfos;
        }

        private static string GetVideoTitle(JObject json)
        {
            //JObject t = JObject.Parse(json["player_response"].ToString());
            JToken title = json["videoDetails"]["title"];

            return title == null ? String.Empty : title.ToString();
        }

        public static void WriteLog(string filename, string c)
        {
            string filePath = "C:/log/" + filename + "_" + DateTime.Now.ToString("ddMMyyyyhhmmssffff") + ".txt";
            Debug.Log("Log written in: " + filePath);
            //Debug.Log("DownloadUrl content saved to " + filePath);
            File.WriteAllText(filePath, c);
        }

        private class ExtractionInfo
        {
            public bool RequiresDecryption { get; set; }

            public Uri Uri { get; set; }
        }
        
        public void TrySkip(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    controller.progressRectangle.rectTransform, eventData.position, eventData.pressEventCamera,
                    out var localPoint))
            {
                float pct = Mathf.InverseLerp(controller.progressRectangle.rectTransform.rect.xMin,
                    controller.progressRectangle.rectTransform.rect.xMax, localPoint.x);
                SkipToPercent(pct);
            }
        }
        
        public void SkipToPercent(float pct)
        {
            if (videoQuality != YoutubeVideoQuality.Standard)
            {
                oldVolume = videoPlayer.GetComponent<AudioSource>().volume;
                videoPlayer.GetComponent<AudioSource>().volume = 0;
            }

            float frame;

            if (videoQuality == YoutubeVideoQuality.Standard)
            {
                frame = videoPlayer.frameCount * pct;
            }
            else
            {
                frame = audioPlayer.frameCount * pct;
            }

            videoPlayer.Pause();
            if (videoQuality != YoutubeVideoQuality.Standard)
                audioPlayer.Pause();
            waitAudioSeek = true;

            if (videoQuality == YoutubeVideoQuality.Standard)
            {
                videoPlayer.frame = (long)frame;
            }
            else
            {
                audioPlayer.frame = (long)frame;
            }

            videoPlayer.Pause();
            if (videoQuality != YoutubeVideoQuality.Standard)
                audioPlayer.Pause();
        }

        IEnumerator VideoSeekCall()
        {
            yield return new WaitForSeconds(1f);
            videoPlayer.time = audioPlayer.time;
        }

        private void VideoSeeked(VideoPlayer source)
        {
            if (!waitAudioSeek)
            {
                StartCoroutine(StartedFromTime ? PlayNowFromTime(2f) : PlayNow());
            }
            else
            {
                StartCoroutine(StartedFromTime ? PlayNowFromTime(2f) : PlayNow());
            }
        }

        bool CheckIfJsIsDownloaded()
        {
            return _jsDownloaded;
        }

        IEnumerator WaitJsDownload()
        {
            if (debug)
                Debug.Log("waiting js");
            yield return new WaitUntil(CheckIfJsIsDownloaded);
        }

        private void AudioSeeked(VideoPlayer source)
        {
            if (!waitAudioSeek)
            {
                StartCoroutine(VideoSeekCall());
            }
            else
            {
                StartCoroutine(VideoSeekCall());
            }
        }

        public virtual void Play()
        {
            //audio issue fix...check all unity versions.
            if (videoQuality != YoutubeVideoQuality.Standard)
            {
                videoPlayer.GetComponent<AudioSource>().volume = oldVolume;
            }
        }

        IEnumerator WaitSync()
        {
            yield return new WaitForSeconds(2f);
            Play();
            Invoke(nameof(VerifyFrames), 2);
        }

        private IEnumerator PlayNow()
        {
            if (videoQuality == YoutubeVideoQuality.Standard)
            {
                yield return new WaitForSeconds(0);
            }
            else
            {
                yield return new WaitForSeconds(1f);
            }

            if (!pauseCalled)
            {
                Play();
                StartCoroutine(ReleaseDrop());
            }
            else
            {
                StopCoroutine(nameof(PlayNow));
            }
        }

        IEnumerator ReleaseDrop()
        {
            yield return new WaitForSeconds(2f);
        }

        IEnumerator PlayNowFromTime(float time)
        {
            yield return new WaitForSeconds(time);
            StartedFromTime = false;
            if (!pauseCalled)
            {
                Play();
            }
            else
            {
                StopCoroutine(nameof(PlayNowFromTime));
            }
        }
    }
}