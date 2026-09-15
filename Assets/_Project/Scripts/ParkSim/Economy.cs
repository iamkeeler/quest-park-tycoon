using UnityEngine;

namespace QuestParkTycoon
{
    /// <summary>
    /// RCT1 economy formulas, preserved as tuning targets (see PRD §4.5).
    /// Static + data-only for Phase 0; a ParkBank MonoBehaviour will own
    /// monthly ticks (wages, loan interest) in Phase 1.
    /// </summary>
    public static class Economy
    {
        public static float Cash = 10000f;
        public static float Loan = 10000f;
        public static float AdmissionFee = 50f;

        /// <summary>
        /// RCT1 rule: per-ride price ≈ excitement rounded down to the tenth,
        /// adjusted by (intensity - excitement) / 10. Nausea does not affect
        /// willingness to pay.
        /// </summary>
        public static float SuggestedRidePrice(float excitement, float intensity)
        {
            float price = Mathf.Floor(excitement * 10f) / 10f;
            price += (intensity - excitement) / 10f;
            return Mathf.Max(0.5f, Mathf.Round(price * 10f) / 10f);
        }

        /// <summary>
        /// RCT1 rule of thumb: max complaint-free entrance fee ≈ parkValue / 2000.
        /// </summary>
        public static float SuggestedEntranceFee(float parkValue)
        {
            return Mathf.Max(0f, Mathf.Floor(parkValue / 2000f));
        }

        /// <summary>RCT1 rule: duplicate ride types lose ~25% value each.</summary>
        public static float DuplicatePenalty(int duplicateCount)
        {
            return Mathf.Pow(0.75f, Mathf.Max(0, duplicateCount));
        }

        /// <summary>Can this guest afford a price? Gate before queueing/buying.</summary>
        public static bool CanAfford(GuestAgent guest, float price)
        {
            return guest != null && guest.wallet >= price;
        }

        /// <summary>
        /// How the park charges a guest: deducts price from the guest's wallet
        /// and adds it to park Cash. Returns the amount actually paid
        /// (0 when the guest can't afford it — caller picks another option).
        /// Used for admission, ride tickets (on boarding), and stall sales.
        /// </summary>
        public static float TakePayment(GuestAgent guest, float price)
        {
            if (guest == null || price <= 0f) return 0f;
            if (guest.wallet < price) return 0f;
            guest.wallet -= price;
            Cash += price;
            return price;
        }
    }
}
