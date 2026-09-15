using System;
using UnityEngine;

namespace QuestParkTycoon.Rides
{
    public enum StallType { Burger, Drinks, CottonCandy, InfoKiosk,
        Bathroom, Fries, Pizza, IceCream, Popcorn, Coffee, Balloon, Souvenir }

    public enum StallEffect { ReduceHunger, ReduceThirst, ReduceBladder, Umbrella, ParkMap, HappinessBoost }

    [Serializable]
    public struct StallItem
    {
        public string itemName;
        public float price;
        public StallEffect effect;
    }

    /// <summary>
    /// Food/drink/service stall. Extends Ride so pricing, value and park
    /// rating flow through the same economy code; stalls never break down
    /// (RCT1) so reliability decay is cosmetic here.
    /// Guest-AI team: call ServeGuest(guest) when a peep reaches the stall.
    /// </summary>
    public sealed class Stall : Ride, IStall, ISimSystem
    {
        public StallType stallType;
        public StallItem[] items = Array.Empty<StallItem>();

        public float lifetimeRevenue;

        protected override void Awake()
        {
            base.Awake(); // RideDirectory registration
            category = RideCategory.Stall;
            isOpen = true; // stalls open immediately once built
            StallDirectory.Register(this);
            var tick = FindObjectOfType<SimTick>();
            if (tick != null) tick.Register(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            StallDirectory.Unregister(this);
        }

        // ---------------- IStall (guest-AI contract) ----------------
        string IStall.StallName => rideName;
        StallType IStall.Type => stallType;
        float IStall.Price
        {
            get => items.Length > 0 ? items[0].price : 0f;
            set { if (items.Length > 0) { var it = items[0]; it.price = value; items[0] = it; } }
        }
        Vector3 IStall.Position => transform.position;
        bool IStall.IsOpen => isOpen;
        bool IStall.Serve(GuestAgent guest) => ServeGuest(guest);

        public override void Tick(float dt)
        {
            if (!isOpen) return;
            reliability = Mathf.Max(0f, reliability - dt * 0.005f); // cosmetic only
        }

        protected override void TickReliability(float dt) { /* stalls don't break down */ }

        /// <summary>
        /// Sell to one guest. Returns true if a sale happened.
        /// RCT1 rules preserved: umbrellas sell at ANY price when raining;
        /// maps mark the guest as not-lost (guest-AI team reads hasMap).
        /// All payments route through Economy.TakePayment (free items skip it).
        /// </summary>
        public bool ServeGuest(GuestAgent guest)
        {
            if (guest == null || !isOpen || items.Length == 0) return false;

            StallItem item = PickItemFor(guest);

            float price = item.price;
            if (item.effect == StallEffect.Umbrella &&
                WeatherSystem.Instance != null && WeatherSystem.Instance.IsRaining)
            {
                price = Mathf.Max(price, 10f); // RCT1: guests buy umbrellas at any price in rain
            }

            if (price > 0f)
            {
                float paid = Economy.TakePayment(guest, price);
                if (paid < price)
                {
                    guest.lastThought = $"I'm not paying that much for {item.itemName}.";
                    return false;
                }
                lifetimeRevenue += paid;
            }

            switch (item.effect)
            {
                case StallEffect.ReduceHunger: guest.hunger = Mathf.Clamp01(guest.hunger - 0.6f); break;
                case StallEffect.ReduceThirst: guest.thirst = Mathf.Clamp01(guest.thirst - 0.7f); break;
                case StallEffect.ReduceBladder: guest.bladder = Mathf.Clamp01(guest.bladder - 1f); break;
                case StallEffect.Umbrella: guest.hasUmbrella = true; guest.happiness = Mathf.Clamp01(guest.happiness + 0.1f); break;
                case StallEffect.ParkMap: guest.hasMap = true; break;
                case StallEffect.HappinessBoost: guest.happiness = Mathf.Clamp01(guest.happiness + 0.15f); break;
            }
            return true;
        }

        private StallItem PickItemFor(GuestAgent guest)
        {
            // Serve the guest's strongest need first; default to the first item.
            foreach (var item in items)
            {
                if (item.effect == StallEffect.ReduceHunger && guest.hunger > 0.4f) return item;
                if (item.effect == StallEffect.ReduceThirst && guest.thirst > 0.4f) return item;
                if (item.effect == StallEffect.ReduceBladder && guest.bladder > 0.5f) return item;
                if (item.effect == StallEffect.Umbrella &&
                    WeatherSystem.Instance != null && WeatherSystem.Instance.IsRaining) return item;
            }
            foreach (var item in items)
            {
                // Souvenirs/balloons are impulse buys for unhappy guests.
                if (item.effect == StallEffect.HappinessBoost && guest.happiness < 0.5f) return item;
            }
            return items[0];
        }

        /// <summary>Factory defaults per stall type (RCT1 build costs).</summary>
        public static Stall Create(StallType type, Transform parent)
        {
            var go = new GameObject(type + " Stall");
            go.transform.SetParent(parent, false);
            var stall = go.AddComponent<Stall>();
            stall.stallType = type;
            // Awake() already ran at AddComponent (before stallType was set),
            // so the rideType mapping lives here, not in Awake().
            stall.rideType = type switch
            {
                StallType.Burger => RideType.BurgerStall,
                StallType.Drinks => RideType.DrinksStall,
                StallType.CottonCandy => RideType.CottonCandyStall,
                StallType.Fries => RideType.FriesStall,
                StallType.Pizza => RideType.PizzaStall,
                StallType.IceCream => RideType.IceCreamStall,
                StallType.Popcorn => RideType.PopcornStall,
                StallType.Coffee => RideType.CoffeeStall,
                StallType.Balloon => RideType.BalloonStall,
                StallType.Souvenir => RideType.SouvenirStall,
                StallType.Bathroom => RideType.Bathroom,
                _ => RideType.InfoKiosk,
            };
            switch (type)
            {
                case StallType.Burger:
                    stall.rideName = "Burger Bar"; stall.buildCost = 300f;
                    stall.items = new[] { new StallItem { itemName = "Burger", price = 1.5f, effect = StallEffect.ReduceHunger } };
                    break;
                case StallType.Drinks:
                    stall.rideName = "Lemonade Stall"; stall.buildCost = 210f;
                    stall.items = new[] { new StallItem { itemName = "Lemonade", price = 1.4f, effect = StallEffect.ReduceThirst } };
                    break;
                case StallType.CottonCandy:
                    stall.rideName = "Cotton Candy Stall"; stall.buildCost = 250f;
                    stall.items = new[] { new StallItem { itemName = "Cotton Candy", price = 1.2f, effect = StallEffect.ReduceHunger } };
                    break;
                case StallType.InfoKiosk:
                    stall.rideName = "Information Kiosk"; stall.buildCost = 250f;
                    stall.items = new[]
                    {
                        new StallItem { itemName = "Park Map", price = 0.5f, effect = StallEffect.ParkMap },
                        new StallItem { itemName = "Umbrella", price = 2.5f, effect = StallEffect.Umbrella },
                    };
                    break;
                case StallType.Fries:
                    stall.rideName = "Fries Stand"; stall.buildCost = 300f;
                    stall.items = new[] { new StallItem { itemName = "Fries", price = 1.6f, effect = StallEffect.ReduceHunger } };
                    break;
                case StallType.Pizza:
                    stall.rideName = "Pizza Place"; stall.buildCost = 350f;
                    stall.items = new[] { new StallItem { itemName = "Pizza Slice", price = 2.2f, effect = StallEffect.ReduceHunger } };
                    break;
                case StallType.IceCream:
                    stall.rideName = "Ice Cream Stall"; stall.buildCost = 280f;
                    stall.items = new[] { new StallItem { itemName = "Ice Cream", price = 1.5f, effect = StallEffect.ReduceHunger } };
                    break;
                case StallType.Popcorn:
                    stall.rideName = "Popcorn Stall"; stall.buildCost = 250f;
                    stall.items = new[] { new StallItem { itemName = "Popcorn", price = 1.3f, effect = StallEffect.ReduceHunger } };
                    break;
                case StallType.Coffee:
                    stall.rideName = "Coffee Shop"; stall.buildCost = 260f;
                    stall.items = new[] { new StallItem { itemName = "Coffee", price = 1.4f, effect = StallEffect.ReduceThirst } };
                    break;
                case StallType.Balloon:
                    stall.rideName = "Balloon Stall"; stall.buildCost = 220f;
                    stall.items = new[] { new StallItem { itemName = "Balloon", price = 2.0f, effect = StallEffect.HappinessBoost } };
                    break;
                case StallType.Souvenir:
                    stall.rideName = "Souvenir Shop"; stall.buildCost = 400f;
                    stall.items = new[] { new StallItem { itemName = "T-Shirt", price = 4.0f, effect = StallEffect.HappinessBoost } };
                    break;
                case StallType.Bathroom:
                    stall.rideName = "Bathrooms"; stall.buildCost = 350f;
                    // RCT1: bathrooms are free; guests just need them to exist.
                    stall.items = new[] { new StallItem { itemName = "Restroom Visit", price = 0f, effect = StallEffect.ReduceBladder } };
                    break;
            }
            return stall;
        }
    }
}
