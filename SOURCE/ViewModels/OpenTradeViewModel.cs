//using SmartCryptoBot.EventHandlers;

namespace SmartCryptoBot.ViewModels {

    public class OpenTradeViewModel {

        private string tradeId;
        public string TradeId {
            get { return tradeId; }
            set {
                tradeId = value;
                //RaisePropertyChangedEvent(nameof(TradeId));
            }
        }

        private string symbol;
        public string Symbol {
            get { return symbol; }
            set {
                symbol = value;
                //RaisePropertyChangedEvent(nameof(Symbol));
            }
        }

        private decimal quantity;
        public decimal Quantity {
            get { return quantity; }
            set {
                expectedsellprice = value;
                //RaisePropertyChangedEvent(nameof(Quantity));
            }
        }

        private decimal buyPrice;
        public decimal BuyPrice {
            get { return buyPrice; }
            set {
                buyPrice = value;
                //RaisePropertyChangedEvent(nameof(BuyPrice));
            }
        }

        private decimal currentPrice;
        public decimal CurrentPrice {
            get { return currentPrice; }
            set {
                currentPrice = value;
                //RaisePropertyChangedEvent(nameof(CurrentPrice));
            }
        }

        private decimal expectedsellprice;
        public decimal ExpectedSellPrice {
            get { return expectedsellprice; }
            set {
                expectedsellprice = value;
                //RaisePropertyChangedEvent(nameof(ExpectedSellPrice));
            }
        }

    }
}
