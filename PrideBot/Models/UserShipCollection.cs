using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PrideBot.Models
{
    public class UserShipCollection : IEnumerable<UserShip>
    {
        IEnumerable<UserShip> UserShips { get; set; }

        public UserShipCollection()
        {
            UserShips = new UserShip[] { };
        }

        public UserShipCollection(IEnumerable<UserShip> userShips)
        {
            UserShips = userShips;
        }

        public UserShip Get(UserShipTier tier)
            => UserShips.FirstOrDefault(a => a.Tier == (int)tier);

        public UserShipCollection Clone() => new UserShipCollection((IEnumerable<UserShip>)UserShips.ToArray().Clone());

        public void Set(UserShipTier tier, UserShip userShip)
            => UserShips = UserShips
            .Except(new UserShip[] { Get(tier) })
            .Concat(new UserShip[] { userShip });

        public bool Has(UserShipTier tier) => Get(tier) != null && !Get(tier).IsEmpty();

        public IEnumerator<UserShip> GetEnumerator() => UserShips.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => UserShips.GetEnumerator();

        public bool Remove(UserShip value) => UserShips.ToList().Remove(value);

        public bool Remove(UserShipTier tier) => Remove(Get(tier));

        public UserShip PrimaryShip { get => Get(UserShipTier.Primary); set => Set(UserShipTier.Primary, value); }
        public UserShip SecondaryShip { get => Get(UserShipTier.Secondary); set => Set(UserShipTier.Secondary, value); }
        public UserShip TertiaryShip { get => Get(UserShipTier.Tertiary); set => Set(UserShipTier.Tertiary, value); }

        public bool HasPrimaryShip => Has(UserShipTier.Primary);
        public bool HasSecondaryShip => Has(UserShipTier.Secondary);
        public bool HasTertiaryShip => Has(UserShipTier.Tertiary);
    }
}
