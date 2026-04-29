using System.Collections;
using UnityEngine;
using UnityEngine.Video;

namespace LightShaft.Scripts
{
    public class YoutubePlayer : YoutubeSimplifiedRequest
    {
        public override void Start()
        {
            base.Start();
            //Register Events
            if (!playUsingInternalDevicePlayer)
            {
                events.OnYoutubeUrlAreReady.AddListener(UrlReadyToUse);
                events.OnVideoFinished.AddListener(OnVideoPlayerFinished);
                events.OnVideoReadyToStart.AddListener(OnVideoLoaded);
            }
        }

        ///<summary>This function is callback only, only will be called when the on url are ready to use.</summary>
        private void UrlReadyToUse(string urlToUse)
        {
            if (loadYoutubeUrlsOnly)
            {
                Debug.Log("Here you can call your external video player if you want, passing that two variables:");
                if (videoQuality != YoutubeVideoQuality.Standard)
                {
                    Debug.Log("Your video Url: " + urlToUse);
                    Debug.Log("Your audio video Url: " + audioUrl);
                }
                else
                {
                    Debug.Log("You video Url:" + urlToUse);
                }
            }
        }

        ///<summary>Get the video title, but it need to be loaded first.</summary>
        public string GetVideoTitle()
        {
            return videoTitle;
        }

        ///<summary>Load the url only, dont play!.</summary>
        public void LoadUrl(string url)
        {
            Stop();
            loadYoutubeUrlsOnly = true;
            PlayYoutubeVideo(url);
        }

        ///<summary>Load the video without play, good for when you want just to prepare the video to play later.</summary>
        public void PreLoadVideo(string url)
        {
            Stop();
            PrepareVideoToPlayLater = true;
            autoPlayOnStart = false;
            PlayYoutubeVideo(url);
        }

        ///<summary>Play the loaded video from time.</summary>
        public void Play(int startTime)
        {
            startFromSecond = true;
            startFromSecondTime = startTime;
            DisableThumbnailObject();
            pauseCalled = false;
            events.OnVideoStarted.Invoke();
            if (videoQuality == YoutubeVideoQuality.Standard)
            {
                videoPlayer.Play();
            }
            else
            {
                videoPlayer.Play();
                audioPlayer.Play();
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

        ///<summary>Load and Play the video from youtube.</summary>
        public void Play(string url)
        {
            Stop();
            PlayYoutubeVideo(url);
        }

        ///<summary>Load and Play a custom playlist.</summary>
        public void Play(string[] playlistUrls)
        {
            Stop();
            customPlaylist = true;
            youtubeUrls = playlistUrls;
            PlayYoutubeVideo(playlistUrls[CurrentUrlIndex]);
        }

        ///<summary>Play the loaded video.</summary>
        public override void Play()
        {
            base.Play();
            events.OnVideoStarted.Invoke();
            DisableThumbnailObject();
            pauseCalled = false;
            if (videoQuality == YoutubeVideoQuality.Standard)
            {
                videoPlayer.Play();
            }
            else
            {
                videoPlayer.Play();
                if (controller.volumeSlider != null)
                    audioPlayer.GetTargetAudioSource(0).volume = controller.volumeSlider.value;
                else
                    audioPlayer.GetTargetAudioSource(0).volume = 1;

                StartCoroutine(DelayPlay());
            }
        }

        ///<summary>Load and Play the video from youtube, starting from desired second.</summary>
        public void Play(string url, int startFrom)
        {
            startFromSecond = true;
            startFromSecondTime = startFrom;
            Stop();
            PlayYoutubeVideo(url);
        }

        ///<summary>Play or Pause the active videoplayer.</summary>
        public void PlayPause()
        {
            if (YoutubeUrlReady && videoPlayer.isPrepared)
            {
                if (!pauseCalled)
                {
                    events.OnVideoPaused.Invoke();
                    Pause();
                }
                else
                {
                    //resume
                    events.OnVideoResumed.Invoke();
                    Play();
                }
            }
        }

        ///<summary>Change the video rendering to fullscreen or back to material renderer.</summary>
        public void ToogleFullsScreenMode()
        {
            FullscreenModeEnabled = !FullscreenModeEnabled;

            if (!FullscreenModeEnabled)
            {
                videoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
                if (videoPlayer.targetCamera == null)
                {
                    videoPlayer.targetCamera = mainCamera;
                }
            }
            else
            {
                videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            }
        }

        ///<summary>Called when the video end.</summary>
        private void OnVideoPlayerFinished()
        {
            if (!FinishedCalled)
            {
                Debug.Log(("video finished..."));
                FinishedCalled = true;
                StartCoroutine(PreventFinishToBeCalledTwoTimes());
                if (!loadYoutubeUrlsOnly)
                {
                    if (videoPlayer.isPrepared)
                    {
                        if (debug)
                            Debug.Log("Finished");
                        if (videoPlayer.isLooping)
                        {
                            videoPlayer.time = 0;
                            videoPlayer.frame = 0;
                            audioPlayer.time = 0;
                            audioPlayer.frame = 0;
                            videoPlayer.Play();
                            audioPlayer.Play();
                        }
                        events.OnVideoFinished.Invoke();

                        if (customPlaylist && autoPlayNextVideo)
                        {
                            Debug.Log("Calling next video of playlist");
                            CallNextUrl();
                        }
                    }
                }
                else
                {
                    if (playUsingInternalDevicePlayer)
                    {
                        events.OnVideoFinished.Invoke();
                    }
                }
            }
        }

        ///<summary>Just a simple callback function to know when the video is loaded and ready to hit play(), you can use the unity events too.</summary>
        private void OnVideoLoaded()
        {
            if (controller.useSliderToProgressVideo)
            {
                if (controller.playbackSlider == null)
                {
                    controller.showPlayerControl = false;  //Disable player controller because there is not playback controller attached;
                }
                else
                {
                    controller.playbackSlider.maxValue = videoQuality != YoutubeVideoQuality.Standard ? Mathf.RoundToInt(audioPlayer.frameCount / audioPlayer.frameRate) : Mathf.RoundToInt(videoPlayer.frameCount / videoPlayer.frameRate);
                }
            }

            if (events != null)
            {
                if (events.videoTimeEvents.Length > 0)
                {
                    foreach (var ev in events.videoTimeEvents)
                    {
                        ev.Called = false; //reset timed events if we have it.
                    }
                }
            }
            Debug.Log("The video is ready to play");
        }

        ///<summary>Call the next url of the playlist.</summary>
        public void CallNextUrl()
        {
            if (!customPlaylist)
                return;
            if ((CurrentUrlIndex + 1) < youtubeUrls.Length)
            {
                CurrentUrlIndex++;
            }
            else
            {
                //reset
                CurrentUrlIndex = 0;
            }

            PlayYoutubeVideo(youtubeUrls[CurrentUrlIndex]);
        }

        public void CallPreviousUrl()
        {
            if (!customPlaylist)
                return;
            if ((CurrentUrlIndex - 1) > 0)
            {
                CurrentUrlIndex--;
            }
            else
            {
                CurrentUrlIndex = 0;
            }
            PlayYoutubeVideo(youtubeUrls[CurrentUrlIndex]);
        }

        //A workaround for mobile bugs.
        private void OnApplicationPause(bool pause)
        {
            if (!playUsingInternalDevicePlayer && !loadYoutubeUrlsOnly)
            {
                if (videoPlayer.isPrepared)
                {
                    if (audioPlayer != null)
                        audioPlayer.Pause();

                    videoPlayer.Pause();
                }
            }
        }

        private void OnApplicationQuit()
        {
            if(videoPlayer != null)
            {
                if (videoPlayer.targetTexture != null)
                    videoPlayer.targetTexture.Release();
            }
            

            if (!playUsingInternalDevicePlayer)
            {
                events.OnYoutubeUrlAreReady.RemoveListener(UrlReadyToUse);
                events.OnVideoFinished.RemoveListener(OnVideoPlayerFinished);
                events.OnVideoReadyToStart.RemoveListener(OnVideoLoaded);
            }
        }

        //A workaround for mobile bugs.
        //private void OnApplicationFocus(bool focus)
        //{
        //    if (focus == true)
        //    {
        //        if (!playUsingInternalDevicePlayer && !loadYoutubeUrlsOnly && !pauseCalled)
        //        {
        //            if (videoPlayer.isPrepared)
        //            {
        //                if (audioPlayer != null)
        //                {
        //                    if (!noAudioAtacched && (videoQuality != YoutubeVideoQuality.STANDARD))
        //                        audioPlayer.Play();
        //                }
        //                videoPlayer.Play();
        //            }
        //        }
        //    }
        //}
        //A workaround for mobile bugs.
        private void OnEnable()
        {
            if (autoPlayOnEnable && !pauseCalled)
            {
                StartCoroutine(WaitThingsGetDone());
            }
        }
        //A workaround for mobile bugs.
        private IEnumerator WaitThingsGetDone()
        {
            yield return new WaitForSeconds(1);
            if (YoutubeUrlReady && videoPlayer.isPrepared)
            {
                Play();
            }
            else
            {
                if (!YoutubeUrlReady)
                    Play(youtubeUrl);
            }
        }
    }
}