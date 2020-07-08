using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ARTEMIS.VideoStream
{
    public class FocusedVideoManager : MonoBehaviour
    {
        public static FocusedVideoManager Instance;

        public List<AdaptiveSizing> SelectionOptions = new List<AdaptiveSizing>();
        public GameObject ExpandedVideo;
        [SerializeField]
        private GameObject FeedInfoParent;
        public FeedInfo FeedInfoDisplay;
        private GameObject FrameToCopy;
        private AdaptiveSizing.Frame frame;

        public struct FeedInfo
        {
            public Text text;
            public GameObject background;

            public FeedInfo(GameObject StreamQualityInfo)
            {
                text = StreamQualityInfo.GetComponentInChildren<Text>();
                background = StreamQualityInfo.GetComponentInChildren<Canvas>().gameObject;
            }

            public void DisplayFrameDelay(float bandwidth, int resolution, float delay)
            {
                text.text = string.Format("<color=#00aaff>Status:</color> Bandwidth {0:0}KBs, Video {1}p, {2:0.0}s delay", bandwidth, resolution, delay);
            }
        }

        /// <summary>
        /// The Video Feed (RawImage) which the expanded video should copy
        /// </summary>
        /// <value>The feed to copy.</value>
        public GameObject FeedToCopy
        {
            get
            {
                return FrameToCopy;
            }
            set
            {
                Debug.Log("Frame To Copy currently set to " + FrameToCopy.transform.parent.name);
                FrameToCopy.GetComponentInParent<AdaptiveSizing>().DelayTimeUpdated -= FeedInfoDisplay.DisplayFrameDelay; // Remove old event listener
                FrameToCopy = value;

                // Set Texture and mesh Size
                ExpandedVideo.GetComponentInChildren<MeshRenderer>().material.mainTexture = value.GetComponent<MeshRenderer>().material.mainTexture;
                ExpandedVideo.GetComponent<AdaptiveSizing>().Resize();

                // Set Colors
                ResetFeedBorders();
                value.GetComponentInParent<AdaptiveSizing>().SetAsActiveFeed(true);

                // Set FeedDisplayInfo data and position
                FrameToCopy.GetComponentInParent<AdaptiveSizing>().DelayTimeUpdated += FeedInfoDisplay.DisplayFrameDelay;
                //FeedInfoDisplay.background.GetComponent<RectTransform>().localPosition = new Vector3(
                //    ExpandedVideo.transform.localPosition.x/2,
                //    FeedInfoDisplay.background.GetComponent<RectTransform>().localPosition.y,
                //    FeedInfoDisplay.background.GetComponent<RectTransform>().localPosition.z
                //);
            }
        }

        void Start()
        {
            Instance = this;
            frame = new AdaptiveSizing.Frame(ExpandedVideo.transform.Find("Frame").gameObject);
            FrameToCopy = SelectionOptions[0].transform.Find("Video").gameObject;
            FeedInfoDisplay = new FeedInfo(FeedInfoParent);
        }

        /// <summary>
        /// Constantly updates the video feed
        /// </summary>
        private void Update()
        {
            try
            {
                Texture tex = FrameToCopy.GetComponent<Renderer>().material.mainTexture;
                ExpandedVideo.GetComponentInChildren<MeshRenderer>().material.mainTexture = tex;
                FeedInfoDisplay.DisplayFrameDelay(-1, tex.width, -1);
            }
            catch (System.NullReferenceException)
            {
                return;
            }

            // Garbage Collection every 10 frames
            if (Time.frameCount % 10 == 0)
            {
                Resources.UnloadUnusedAssets();
                System.GC.Collect();
            }
        }

        /// <summary>
        /// Resets each Selectable Feed status as being active
        /// </summary>
        public void ResetFeedBorders()
        {
            foreach (AdaptiveSizing feed in SelectionOptions)
            {
                feed.SetAsActiveFeed(false);
            }
        }
    }
}