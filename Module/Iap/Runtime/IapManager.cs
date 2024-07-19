#if VIRTUESKY_IAP
using System;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using VirtueSky.Ads;
using VirtueSky.Core;
using VirtueSky.Inspector;
using VirtueSky.Misc;

namespace VirtueSky.Iap
{
    [EditorIcon("icon_manager"), HideMonoScript]
    public class IapManager : Singleton<IapManager>, IDetailedStoreListener
    {
        public static event Action<string> OnPurchaseSucceedEvent;
        public static event Action<string> OnPurchaseFailedEvent;

        private IStoreController _controller;
        private IExtensionProvider _extensionProvider;
        private bool _IsInitialized { get; set; }
        private IapSettings iapSettings;

        private void Start()
        {
            iapSettings = IapSettings.Instance;
            Init();
        }

        private async void Init()
        {
            var options = new InitializationOptions().SetEnvironmentName("production");
            await UnityServices.InitializeAsync(options);
            InitImpl();
        }

        void InitImpl()
        {
            if (_IsInitialized) return;
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            RequestProductData(builder);
            builder.Configure<IGooglePlayConfiguration>();

            UnityPurchasing.Initialize(this, builder);
            _IsInitialized = true;
        }


        private void PurchaseProductInternal(IapDataProduct product)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            _controller?.InitiatePurchase(product.Id);
#elif UNITY_EDITOR
            InternalPurchaseSuccess(product.Id);
#endif
        }

        private void RequestProductData(ConfigurationBuilder builder)
        {
            foreach (var p in iapSettings.IapDataProducts)
            {
                builder.AddProduct(p.Id, ConvertProductType(p.iapProductType));
            }
        }

        private void InternalPurchaseFailed(string id)
        {
            AdStatic.OnChangePreventDisplayAppOpenEvent?.Invoke(false);
            foreach (var product in iapSettings.IapDataProducts)
            {
                if (product.Id != id) continue;
                OnPurchaseFailedEvent?.Invoke(product.Id);
                Common.CallActionAndClean(ref product.purchaseFailedCallback);
            }
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            switch (error)
            {
                case InitializationFailureReason.AppNotKnown:
                    Debug.LogError("Is your App correctly uploaded on the relevant publisher console?");
                    break;
                case InitializationFailureReason.PurchasingUnavailable:
                    Debug.LogWarning("In App Purchases disabled in device settings!");
                    break;
                case InitializationFailureReason.NoProductsAvailable:
                    Debug.LogWarning("No products available for purchase!");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(error), error, null);
            }
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            OnInitializeFailed(error);
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs purchaseEvent)
        {
            if (iapSettings.IsValidatePurchase)
            {
                bool validatedPurchase = true;
#if (UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            var validator =
                new UnityEngine.Purchasing.Security.CrossPlatformValidator(UnityEngine.Purchasing.Security.GooglePlayTangle.Data(),
                    UnityEngine.Purchasing.Security.AppleTangle.Data(), Application.identifier);

            try
            {
                // On Google Play, result has a single product ID.
                // On Apple stores, receipts contain multiple products.
                var result = validator.Validate(purchaseEvent.purchasedProduct.receipt);
                Debug.Log("Receipt is valid");
            }
            catch (UnityEngine.Purchasing.Security.IAPSecurityException)
            {
                Debug.Log("Invalid receipt, not unlocking content");
                validatedPurchase = false;
            }
#endif
                if (validatedPurchase) PurchaseVerified(purchaseEvent);
            }
            else
            {
                PurchaseVerified(purchaseEvent);
            }

            return PurchaseProcessingResult.Complete;
        }

        void PurchaseVerified(PurchaseEventArgs purchaseEvent)
        {
            AdStatic.OnChangePreventDisplayAppOpenEvent?.Invoke(false);
            InternalPurchaseSuccess(purchaseEvent.purchasedProduct.definition.id);
        }

        void InternalPurchaseSuccess(string id)
        {
            foreach (var product in iapSettings.IapDataProducts)
            {
                if (product.Id != id) continue;
                OnPurchaseSucceedEvent?.Invoke(product.Id);
                Common.CallActionAndClean(ref product.purchaseSuccessCallback);
            }
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            InternalPurchaseFailed(product.definition.id);
        }

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            _controller = controller;
            _extensionProvider = extensions;

#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (var product in _controller.products.all)
            {
                if (product != null && !string.IsNullOrEmpty(product.transactionID))
                    _controller.ConfirmPendingPurchase(product);
            }
#endif
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            InternalPurchaseFailed(product.definition.id);
        }

#if UNITY_IOS
        private void InternalRestorePurchase()
        {
            if (!IsInitialized)
            {
                Debug.Log("Restore purchases fail. not initialized!");
                return;
            }

            if (Application.platform == RuntimePlatform.IPhonePlayer ||
                Application.platform == RuntimePlatform.OSXPlayer)
            {
                Debug.Log("Restore purchase started ...");

                var storeProvider = _extensionProvider.GetExtension<IAppleExtensions>();
                storeProvider.RestoreTransactions(_ =>
                {
                    // no purchase are avaiable to restore
                    Debug.Log("Restore purchase continuting: " + _ +
                              ". If no further messages, no purchase available to restore.");
                });
            }
            else
            {
                Debug.Log("Restore purchase fail. not supported on this platform. current = " + Application.platform);
            }
        }
#endif

        ProductType ConvertProductType(IapProductType iapProductType)
        {
            switch (iapProductType)
            {
                case IapProductType.Consumable:
                    return ProductType.Consumable;
                case IapProductType.NonConsumable:
                    return ProductType.NonConsumable;
                case IapProductType.Subscription:
                    return ProductType.Subscription;
            }

            return ProductType.Consumable;
        }


        #region Internal API

        private IapDataProduct InternalPurchaseProduct(string id)
        {
            AdStatic.OnChangePreventDisplayAppOpenEvent?.Invoke(true);
            var product = iapSettings.GetIapProduct(id);
            PurchaseProductInternal(product);
            return product;
        }

        private IapDataProduct InternalPurchaseProduct(IapDataProduct product)
        {
            AdStatic.OnChangePreventDisplayAppOpenEvent?.Invoke(true);
            PurchaseProductInternal(product);
            return product;
        }

        private bool InternalIsPurchasedProduct(IapDataProduct product)
        {
            if (_controller == null) return false;
            return ConvertProductType(product.iapProductType) == ProductType.NonConsumable &&
                   _controller.products.WithID(product.Id).hasReceipt;
        }

        private bool InternalIsPurchasedProduct(string id)
        {
            if (_controller == null) return false;
            return ConvertProductType(iapSettings.GetIapProduct(id).iapProductType) == ProductType.NonConsumable &&
                   _controller.products.WithID(id).hasReceipt;
        }

        private Product InternalGetProduct(IapDataProduct product)
        {
            if (_controller == null) return null;
            return _controller.products.WithID(product.Id);
        }

        private Product InternalGetProduct(string id)
        {
            if (_controller == null) return null;
            return _controller.products.WithID(id);
        }

        #endregion

        #region Public API

        public static IapDataProduct PurchaseProduct(string id) => Instance.InternalPurchaseProduct(id);

        public static IapDataProduct PurchaseProduct(IapDataProduct product) =>
            Instance.InternalPurchaseProduct(product);

        public static bool IsPurchasedProduct(IapDataProduct product) => Instance.InternalIsPurchasedProduct(product);
        public static bool IsPurchasedProduct(string id) => Instance.InternalIsPurchasedProduct(id);

        public static Product GetProduct(IapDataProduct product) => Instance.InternalGetProduct(product);
        public static Product GetProduct(string id) => Instance.InternalGetProduct(id);


#if UNITY_IOS
        public static void RestorePurchase() => Instance.InternalRestorePurchase();
#endif
        public static bool IsInitialized => Instance._IsInitialized;

        #endregion
    }
}

#endif