using System;
using System.Collections;
using UnityEngine;
using VirtueSky.Core;
using VirtueSky.Inspector;

#if UNITY_IOS
using UnityEngine.iOS;
#elif UNITY_ANDROID && VIRTUESKY_RATING
using Google.Play.Review;

#endif

namespace VirtueSky.Rating
{
    [EditorIcon("icon_manager"), HideMonoScript]
    public class RatingManager : MonoBehaviour
    {
        [SerializeField] private bool dontDestroyOnLoad;
        [Space, SerializeField] private bool autoInit;
        private static RatingManager ins;
        private bool InternalRatingInitialize { get; set; }

#if UNITY_ANDROID && VIRTUESKY_RATING
        private ReviewManager _reviewManager;
        private PlayReviewInfo _playReviewInfo;
        private Coroutine _coroutine;
#endif
        private void Awake()
        {
            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (ins == null)
            {
                ins = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            InternalRatingInitialize = false;
            if (autoInit)
            {
                InternalInitRateAndReview();
            }
        }

        private void InternalInitRateAndReview()
        {
            if (InternalRatingInitialize) return;
            if (!Application.isMobilePlatform) return;
#if UNITY_ANDROID && VIRTUESKY_RATING
            _coroutine = App.StartCoroutine(InitReview());
#endif
            InternalRatingInitialize = true;
        }

        private void InternalRateAndReview()
        {
            if (!Application.isMobilePlatform) return;

#if UNITY_ANDROID && VIRTUESKY_RATING
            App.StartCoroutine(LaunchReview());
#elif UNITY_IOS
            Device.RequestStoreReview();
#endif
        }

#if UNITY_ANDROID && VIRTUESKY_RATING
        private IEnumerator InitReview(bool force = false)
        {
            if (_reviewManager == null) _reviewManager = new ReviewManager();

            var requestFlowOperation = _reviewManager.RequestReviewFlow();
            yield return requestFlowOperation;
            if (requestFlowOperation.Error != ReviewErrorCode.NoError)
            {
                if (force) DirectlyOpen();
                yield break;
            }

            _playReviewInfo = requestFlowOperation.GetResult();
        }

        public IEnumerator LaunchReview()
        {
            if (_playReviewInfo == null)
            {
                if (_coroutine != null) App.StopCoroutine(_coroutine);
                yield return App.StartCoroutine(InitReview(true));
            }

            var launchFlowOperation = _reviewManager.LaunchReviewFlow(_playReviewInfo);
            yield return launchFlowOperation;
            _playReviewInfo = null;
            if (launchFlowOperation.Error != ReviewErrorCode.NoError)
            {
                DirectlyOpen();
                yield break;
            }
        }
#endif
        private void DirectlyOpen()
        {
            Application.OpenURL($"https://play.google.com/store/apps/details?id={Application.identifier}");
        }

        public static bool RatingInitialize => ins.InternalRatingInitialize;
        public static void InitRateAndReview() => ins.InternalInitRateAndReview();
        public static void RateAndReview() => ins.InternalRateAndReview();
    }
}