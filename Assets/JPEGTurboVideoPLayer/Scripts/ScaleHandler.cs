using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ARTEMIS.VideoStream
{
    public class ScaleHandler : MonoBehaviour
    {
        public GameObject UIBlock;

        private Vector3 HandleStartPosition;
        private Vector3 HandleStartLocalPosition;
        private Vector3 BlockStartPosition;
        private Vector3 BlockStartScale;

        private float deltaStart;

        void Start()
        {
            HandleStartPosition = transform.position;
            HandleStartLocalPosition = transform.localPosition;
            BlockStartPosition = UIBlock.transform.position;
            BlockStartScale = UIBlock.transform.localScale;

            // Calculate delta between start positions
            deltaStart = Vector3.Magnitude(HandleStartPosition - BlockStartPosition);
        }

        void Update()
        {
            if (transform.hasChanged)
            {
                // Calculate Delta between current positions
                float deltaCurrent = Vector3.Magnitude(transform.position - BlockStartPosition);

                // Multiply Block's initial scale by difference in deltas
                float multiplier = deltaCurrent / deltaStart;
                UIBlock.transform.localScale = BlockStartScale * multiplier;

                // Keep Handle in corner of UI
                transform.localPosition = HandleStartLocalPosition;

                transform.hasChanged = false;
            }
        }
    }
}
