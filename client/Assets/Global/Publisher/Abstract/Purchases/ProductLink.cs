using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Global.Publisher
{
    public class ProductLink : EnvAsset, IProductLink
    {
        [SerializeField] private PaymentMethod _paymentMethod;
        [SerializeField] private string _id;
        [SerializeField] private Sprite _shopIcon;
        [ShowIf("_paymentMethod", PaymentMethod.Currency)] [SerializeField] private int _price;


        public Sprite ShopIcon => _shopIcon;
        public string Id => _id;
        public int Price => _price;
        public PaymentMethod PaymentMethod => _paymentMethod;

        public void UpdatePrice(int price)
        {
            _price = price;
        }
    }
}