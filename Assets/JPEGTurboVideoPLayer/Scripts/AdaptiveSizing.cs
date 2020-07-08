using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ARTEMIS.VideoStream
{
    public class AdaptiveSizing : MonoBehaviour
    {
        /// <summary>
        /// Video Stream's Frame - the border around the feed which helps distinguish it from the background
        /// </summary>
        public struct Frame
        {
            public GameObject left;
            public GameObject right;
            public GameObject top;
            public GameObject bottom;

            public Frame(GameObject parent)
            {
                left = parent.transform.Find("Left").gameObject;
                right = parent.transform.Find("Right").gameObject;
                top = parent.transform.Find("Top").gameObject;
                bottom = parent.transform.Find("Bottom").gameObject;
            }

            /// <summary>
            /// Resizes the frame to fit in a new width
            /// </summary>
            /// <param name="width">Width.</param>
            public void SetWidth(float width)
            {
                // Scale Top
                top.transform.localScale = new Vector3(
                    width - 2 * left.transform.localScale.x,
                    top.transform.localScale.y,
                    top.transform.localScale.z
                );

                // Scale Bottom
                bottom.transform.localScale = new Vector3(
                    width - 2 * left.transform.localScale.x,
                    bottom.transform.localScale.y,
                    bottom.transform.localScale.z
                );

                // Move Right
                right.transform.localPosition = new Vector3(
                    width / 2 - left.transform.localScale.x * 1.5f,
                    right.transform.localPosition.y,
                    right.transform.localPosition.z
                );

                // Move Left
                left.transform.localPosition = new Vector3(
                    -width / 2 + left.transform.localScale.x * 1.5f,
                    left.transform.localPosition.y,
                    left.transform.localPosition.z
                );
            }
        }

        [Tooltip("Will autoset to first child called 'Video' if not manually set")]
        public GameObject VideoPlane;
        [Tooltip("Will autoset to first child called 'Frame' if not manually set")]
        public GameObject VideoFrameOverride;
        public Frame VideoFrame;
        [Tooltip("Will autoset to first child called 'Canvas' if not manually set")]
        public GameObject Canvas;
        public bool IsLeftFrame;
        public bool NoRecenter = false;
        public bool NoResizeCanvas = false;
        [HideInInspector]
        public Vector3 Origin;
        [HideInInspector]
        public Vector3 OriginalSize;
        public event System.Action<float, int, float> DelayTimeUpdated; // TODO: Set when this gets invoked

        void Start()
        {
            // Get VideoPlane
            if (this.VideoPlane == null)
            {
                this.VideoPlane = transform.Find("Video").gameObject;
            }

            // Get VideoFrame
            if (this.VideoFrameOverride == null) {
                this.VideoFrame = new Frame(transform.Find("Frame").gameObject);
            } else
            {
                this.VideoFrame = new Frame(VideoFrameOverride);
            }

            // Get Canvas
            if (this.Canvas == null)
            {
                this.Canvas = transform.Find("Canvas").gameObject;
            }
            Origin = this.transform.localPosition;
            OriginalSize = this.transform.localScale;

            DelayTimeUpdated += (obj1, obj2, obj3) => { }; //TODO: Remove this OnDestroy

            if (!transform.parent.name.Equals("Expanded Video")) // Don't resize if Expanded Video
            {
                Resize();
            }
        }

        public void DisplayDelayRate(float streamBandwidth, int videoResolution, float secondsDelayed)
        {
            DelayTimeUpdated.Invoke(streamBandwidth, videoResolution, secondsDelayed);
        }

        /// <summary>
        /// Resize the Frame to match the incoming feed.
        /// </summary>
        public void Resize()
        {
            // Reset to Origin Position
            transform.localPosition = Origin;

            // Get Image dimensions
            int textureWidth = VideoPlane.GetComponent<MeshRenderer>().material.mainTexture.width;
            int textureHeight = VideoPlane.GetComponent<MeshRenderer>().material.mainTexture.height;

            Vector3 planeSize = VideoPlane.transform.localScale;
            float planeWidth = planeSize.x;
            float planeHeight = planeSize.z;

            // Calculate and Set the VideoPlane size to scale from the texture
            float newWidth = textureWidth * planeHeight / textureHeight;
            VideoPlane.transform.localScale = new Vector3(newWidth, planeSize.y, planeHeight);

            // Set Canvas Size
            if (!NoResizeCanvas)
                Canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(newWidth * 10, planeHeight * 10);

            // Set Frame
            float frameWidth = textureWidth * VideoFrame.right.transform.localScale.y / textureHeight;
            VideoFrame.SetWidth(frameWidth);

            // Reposition Everything to keep border between Video Frames
            if (NoRecenter) { return; }

            float offset = (frameWidth - OriginalSize.x) / 2 * ((IsLeftFrame) ? -1 : 1); // Shift left feeds left, don't shift right feeds
            transform.localPosition = new Vector3(
                transform.localPosition.x + offset,
                transform.localPosition.y,
                transform.localPosition.z
            );
        }

        /// <summary>
        /// Set the Expanded Video to copy whatever this feed displays.
        /// Sets the color of this Video's frame to indicate selection.
        /// </summary>
        public void Expand()
        {
            FocusedVideoManager.Instance.FeedToCopy = VideoPlane; // Will set Frames back to white
        }

        /// <summary>
        /// Pauses the video stream
        /// Doesn't actually stop streaming, 
        /// just stops updating the displayed texture
        /// </summary>
        public void Pause()
        {
            VideoPlane.GetComponentInChildren<VideoStreamReceiver>().UpdateTexture = false;
        }


        public void Play()
        {
            VideoPlane.GetComponentInChildren<VideoStreamReceiver>().UpdateTexture = true;
        }

        /// <summary>
        /// Set whether the feed is currently being streamed to the expanded video.
        /// this causes the material of the frame to change.
        /// </summary>
        /// <param name="selected">If set to <c>true</c> selected.</param>
        public void SetAsActiveFeed(bool selected)
        {
            if (selected) // Set Frame Red
            {
                VideoFrame.left.GetComponent<Renderer>().material.SetColor("_Color", Color.red);
                VideoFrame.right.GetComponent<Renderer>().material.SetColor("_Color", Color.red);
                VideoFrame.top.GetComponent<Renderer>().material.SetColor("_Color", Color.red);
                VideoFrame.bottom.GetComponent<Renderer>().material.SetColor("_Color", Color.red);
            }
            else // Set Frame White
            {
                VideoFrame.left.GetComponent<Renderer>().material.SetColor("_Color", Color.white);
                VideoFrame.right.GetComponent<Renderer>().material.SetColor("_Color", Color.white);
                VideoFrame.top.GetComponent<Renderer>().material.SetColor("_Color", Color.white);
                VideoFrame.bottom.GetComponent<Renderer>().material.SetColor("_Color", Color.white);
            }
        }
    }
}