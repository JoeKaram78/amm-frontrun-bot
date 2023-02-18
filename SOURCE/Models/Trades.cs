using System;
using SqlNado;

namespace SmartCryptoBot.Models {

    [SQLiteTable]
    public class Trades {

        [SQLiteColumn(IsPrimaryKey = true)]
        public string Id { get; set; }

        public string Symbol { get; set; }

        public decimal BuyPrice { get; set; }

        public decimal ExpectedSellPrice { get; set; }

        public decimal SellPrice { get; set; }

        public decimal Quantity { get; set; }

        public decimal BuyQuoteTotalPrice { get; set; }

        public decimal SellQuoteTotalPrice { get; set; }

        public int TradeState { get; set; }

        public DateTime BuyTradeDateTime { get; set; }

        public DateTime SellTradeDateTime { get; set; }
    }
}
