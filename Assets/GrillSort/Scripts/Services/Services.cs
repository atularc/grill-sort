using System;
using UnityEngine;

namespace GrillSort
{
    /// <summary>Ad network wrapper. Gameplay only talks to this, so the SDK can change without touching gameplay.</summary>
    public interface IAdsService
    {
        void Init();
        /// <summary>Calls back true only when the reward was earned.</summary>
        void ShowRewarded(string placement, Action<bool> done);
        void ShowInterstitial(string placement, Action done);
    }

    public struct PurchaseResult { public bool Ok, Cancelled; }

    /// <summary>In-app purchase wrapper (Google Play Billing / StoreKit behind it).</summary>
    public interface IIapService
    {
        bool Available { get; }
        void Init(Action<bool> ready);
        string Price(string productId);
        void Buy(string productId, Action<PurchaseResult> done);
        /// <summary>Restores non-consumables (needed on iOS: Settings > Restore Purchases). done(true) if anything came back.</summary>
        void Restore(Action<bool> done);
    }

    /// <summary>Placeholder until real ad unit ids exist: every rewarded ad "succeeds" immediately.</summary>
    public sealed class PlaceholderAds : IAdsService
    {
        public void Init() => Debug.Log("[Ads] placeholder init");
        public void ShowRewarded(string placement, Action<bool> done) { Debug.Log($"[Ads] rewarded '{placement}' (placeholder: granted)"); done?.Invoke(true); }
        public void ShowInterstitial(string placement, Action done) { Debug.Log($"[Ads] interstitial '{placement}' (placeholder)"); done?.Invoke(); }
    }

    /// <summary>Placeholder store: nothing is for sale yet.</summary>
    public sealed class PlaceholderIap : IIapService
    {
        public bool Available => false;
        public void Init(Action<bool> ready) => ready?.Invoke(false);
        public string Price(string productId) => "-";
        public void Buy(string productId, Action<PurchaseResult> done) => done?.Invoke(new PurchaseResult { Cancelled = true });
        public void Restore(Action<bool> done) { Debug.Log("[IAP] restore (placeholder: nothing to restore)"); done?.Invoke(false); }
    }

    public static class Services
    {
        public static IAdsService Ads = new PlaceholderAds();
        public static IIapService Iap = new PlaceholderIap();
    }
}
