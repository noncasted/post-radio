using UnityEngine;

namespace Global.Publisher
{
    public interface IProductLink
    {
        Sprite ShopIcon { get; }
        string Id { get; }
        int Price { get; }
        PaymentMethod PaymentMethod { get; }

        void UpdatePrice(int price);
    }
}