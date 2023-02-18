using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Binance.Net;
using Binance.Net.Objects;
using SmartCryptoBot.ViewModels;
using SmartCryptoBot.Libraries;
using SmartCryptoBot.Models;
using Newtonsoft.Json;

namespace SmartCryptoBot.Helpers {

    public class BinanceHelper {

        #region Properties        
        ILogger _logger = new Logger(typeof(BinanceHelper));

        private SQLiteDbHelper dbHelper = new SQLiteDbHelper();
        private Dictionary<string, BinanceStreamSubscription> subscriptionList = new Dictionary<string, BinanceStreamSubscription>();
        #endregion

        public bool SetBinanceApiCredentials(BinanceClient client) {
            bool isGood = false;
            try {
                string ApiKey = dbHelper.GetSettingByKey("ApiKey");
                string ApiSecret = dbHelper.GetSettingByKey("ApiSecret");
                if (!string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(ApiSecret)) {
                    client.SetApiCredentials(ApiKey, ApiSecret);
                    isGood = true;
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return isGood;
        }

        // Fixed symbol value "BTC" for now..
        public string GetBaseCurrencySymbol() {
            return "BTC";
        }

        // Fixed 1 percent for now..
        public decimal GetMinProfitPercent() {
            return 1m;
        }

        // Fixed 0.1 percent for binance..
        public decimal GetExchangeTradingFeePercent() {
            return 0.1m;
        }

        public BinanceAccountInfo GetAccountData(BinanceClient client) {
            BinanceAccountInfo accountInfo = new BinanceAccountInfo();
            try {
                var accountInfoData = client.GetAccountInfo();
                if (accountInfoData.Error != null) {
                    _logger.LogError(accountInfoData.Error.Message);
                } else {
                    accountInfo = accountInfoData.Data;
                    foreach (BinanceBalance item in accountInfo.Balances)
                    {
                        Console.WriteLine(item.Asset + "   " + item.Total );
                    }

                    Console.WriteLine("withdraw: " + accountInfo.CanWithdraw);
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return accountInfo;
        }

        public BinancePrice[] GetPrices(BinanceClient client) {
            BinancePrice[] allPrices = { };
            try {
                var allPricesData = client.GetAllPrices();
                if (allPricesData.Error != null) {
                    _logger.LogError(allPricesData.Error.Message);
                } else {
                    allPrices = allPricesData.Data;
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return allPrices;
        }

        public Binance24HPrice Get24hrHistory(BinanceClient client, string symbol) {
            Binance24HPrice pair24hr = new Binance24HPrice();
            try {
                var pair24hrData = client.Get24HPrice(symbol);
                if (pair24hrData.Error != null) {
                    _logger.LogError(pair24hrData.Error.Message);
                } else {
                    pair24hr = pair24hrData.Data;
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return pair24hr;
        }

        public BinanceExchangeInfo GetExchangeInfo(BinanceClient client) {
            BinanceExchangeInfo exchangeInfo = new BinanceExchangeInfo();
            try {
                var exchangeData = client.GetExchangeInfo();
                if (exchangeData.Error != null) {
                    _logger.LogError(exchangeData.Error.Message);
                } else {
                    exchangeInfo = exchangeData.Data;
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return exchangeInfo;
        }

        public decimal GetBaseCurrencyBalance(BinanceClient client, string symbol = "") {
            decimal baseBalance = 0;
            BinanceAccountInfo accountInfo = new BinanceAccountInfo();
            try {
                if (string.IsNullOrEmpty(symbol)) {
                    symbol = GetBaseCurrencySymbol();
                }

                accountInfo = GetAccountData(client);
                if (accountInfo.Balances != null && accountInfo.Balances.Count > 0) {
                    var baseCurrencyData = accountInfo.Balances.Where(a => a.Asset == symbol).FirstOrDefault();
                    if (baseCurrencyData != null) {
                        baseBalance = baseCurrencyData.Free;
                    }
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return baseBalance;
        }

        public void UnsubscribeFromAllStreams(BinanceSocketClient socketClient) {
            try {
                var streamsList = subscriptionList.Keys.ToList();
                foreach (var streamKey in streamsList) {
                    BinanceStreamSubscription streamSubscription = GetSubscriptionByKey(streamKey);
                    if (streamSubscription != null) {
                        socketClient.UnsubscribeFromStream(streamSubscription);
                        subscriptionList.Remove(streamKey);
                    }
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
        }

        public void UnsubscribeFromStream(string key, BinanceSocketClient socketClient) {
            try {
                BinanceStreamSubscription streamSubscription = GetSubscriptionByKey(key);
                if (streamSubscription != null) {
                    socketClient.UnsubscribeFromStream(streamSubscription);
                    subscriptionList.Remove(key);
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
        }

        public void AddToSubscriptionList(string key, BinanceStreamSubscription subscription) {
            try {
                if (GetSubscriptionByKey(key) == null) {
                    subscriptionList.Add(key, subscription);
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
        }

        public BinanceStreamSubscription GetSubscriptionByKey(string key) {
            BinanceStreamSubscription streamSubscription = new BinanceStreamSubscription();
            try {
                subscriptionList.TryGetValue(key, out streamSubscription);
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return streamSubscription;
        }

        #region Helper Methods
        public List<BinancePrice> GetSymbolsByBaseCurrency(BinanceClient client) {
            List<BinancePrice> pairsList = new List<BinancePrice>();
            try {
                string baseCurrencySymbol = GetBaseCurrencySymbol();
                BinancePrice[] allPairs = GetPrices(client);
                pairsList = allPairs.Where(p => p.Symbol.EndsWith(baseCurrencySymbol)).ToList();
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return pairsList;
        }

        public bool CanUserBuy(decimal baseBalance) {
            bool canBuy = false;
            try {
                //decimal baseBalance = GetBaseCurrencyBalance(client);
                decimal tradingLimit = Convert.ToDecimal(dbHelper.GetSettingByKey("TradingLimitPerPair"));
                canBuy = baseBalance >= tradingLimit;
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return canBuy;
        }
        //public bool CanUserBuy(BinanceClient client) {
        //    bool canBuy = false;
        //    try {
        //        decimal baseBalance = GetBaseCurrencyBalance(client);
        //        decimal tradingLimit = Convert.ToDecimal(dbHelper.GetSettingByKey("TradingLimitPerPair"));
        //        canBuy = baseBalance >= tradingLimit;
        //    } catch (Exception ex) {
        //        _logger.LogException(ex);
        //    }
        //    return canBuy; //change to canBuy
        //}

        //don't allow if it exceeded max allowed limit or already exists
        public bool IsMaxPairExistsInOpenTrades(string pair) {
            bool isExist = false;
            decimal maxOpenOrdersAllowed = 10;
            try {
                var openTrades = dbHelper.GetOpenTrades();
                if ((openTrades.Count >= maxOpenOrdersAllowed) || (openTrades.Where(a => a.Symbol == pair).Count() > 0)) {
                    isExist = true;
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return isExist;
        }
        #endregion

        #region Buying Strategies
        public Trades GetBestMatchingPair(BinanceClient client, Strategy strategy) {
            Trades pair = new Trades();
            switch (strategy) {
                case Strategy.Smart:
                    pair = GetPairBySmartStrategy(client);
                    break;
            }
            return pair;
        }

        private Trades GetPairBySmartStrategy(BinanceClient client) {
            Trades pairSmart = new Trades();
            decimal maxPercent = 25, minPercent = -25, minVolume = 250; //filter criteria
            try {
                List<Binance24HPrice> historicPairsList = GetPiarsHistoricData(client).OrderBy(d => d.PriceChangePercent).ToList();
                foreach (Binance24HPrice pair in historicPairsList) {
                    //check for smart stretegy criterias..
                    if (pair.PriceChangePercent < maxPercent && pair.PriceChangePercent >= minPercent && pair.QuoteVolume > minVolume) {
                        //check if pair isn't already added or max limit exceeded for openTrades..
                        if (!IsMaxPairExistsInOpenTrades(pair.Symbol)) {
                            var expectedBuyPrice = GetBuyPrice(pair.WeightedAveragePrice);
                            if (pair.AskPrice <= expectedBuyPrice) { //Bid/Buy price must be <= expected buy price
                                //check for valid quantity filters for binance..
                                decimal validQuantity = GetFilteredBuyQuantity(client, pair.Symbol, pair.AskPrice);
                                var openOrders = dbHelper.GetOpenTrades();
                                var symbolOpenTrade = openOrders.Where(p => p.Symbol == pair.Symbol).Count();
                                if (validQuantity > 0 && symbolOpenTrade == 0) {
                                    pairSmart.Symbol = pair.Symbol;
                                    pairSmart.Quantity = validQuantity;
                                    pairSmart.BuyPrice = expectedBuyPrice;
                                    _logger.LogInfoMessage("Symbol: " + pair.Symbol + "  ::> 24hr Change: " + pair.PriceChangePercent + "  ::> Buy Price: " + expectedBuyPrice + "   ::> Qty: " + validQuantity);
                                }
                                return pairSmart;
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return pairSmart;
        }

        private List<Binance24HPrice> GetPiarsHistoricData(BinanceClient client) {
            List<Binance24HPrice> lstBinanceHistoricDetails = new List<Binance24HPrice>();
            try {
                var pairsList = GetSymbolsByBaseCurrency(client);
                foreach (BinancePrice pair in pairsList) {
                    Binance24HPrice pairs24hr = Get24hrHistory(client, pair.Symbol);
                    if (pairs24hr != null) {
                        lstBinanceHistoricDetails.Add(pairs24hr);
                    }
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return lstBinanceHistoricDetails;
        }
        //private Trades GetPairBySmartStrategy(BinanceClient client) {
        //    Trades pairSmart = new Trades();
        //    decimal maxPercent = 25, minPercent = 1, minVolume = 125; //filter criteria
        //    try {
        //        List<Binance24HPrice> lstBinanceHistoricDetails = new List<Binance24HPrice>();
        //        var pairsList = GetSymbolsByBaseCurrency(client);
        //        foreach (BinancePrice pair in pairsList) {
        //            Binance24HPrice pairs24hr = Get24hrHistory(client, pair.Symbol);
        //            if (pairs24hr != null) {
        //                lstBinanceHistoricDetails.Add(pairs24hr);                        
        //            }
        //        }

        //        var bestPair = lstBinanceHistoricDetails.OrderByDescending(d => d.PriceChangePercent);
        //        if (bestPair != null) {
        //            //check for smart stretegy criterias..
        //                if (pairs24hr.PriceChangePercent < maxPercent && pairs24hr.PriceChangePercent >= minPercent && pairs24hr.QuoteVolume > minVolume) {
        //                    //check if pair isn't already added or max limit exceeded for openTrades..
        //                    if (!IsMaxPairExistsInOpenTrades(pairs24hr.Symbol)) {
        //                        //check for valid quantity filters for binance..
        //                        decimal validQuantity = GetFilteredBuyQuantity(client, pair.Symbol, pair.Price);
        //                        var expectedBuyPrice = GetBuyPrice(pairs24hr.WeightedAveragePrice);
        //                        if (validQuantity > 0 && pairs24hr.AskPrice <= expectedBuyPrice) { //Bid/Buy price must be <= expected buy price

        //                        }
        //                    }
        //                }
        //            pairSmart.Symbol = bestPair.Symbol;
        //            pairSmart.Quantity = validQuantity;
        //            pairSmart.BuyPrice = expectedBuyPrice;
        //            _logger.LogInfoMessage("Symbol: " + bestPair.Symbol + "  ::> 24hr Change: " + pairs24hr.PriceChangePercent + "  ::> Buy Price: " + expectedBuyPrice + "   ::> Qty: " + validQuantity);
        //            return pairSmart;
        //        }
        //    } catch (Exception ex) {
        //        _logger.LogException(ex);
        //    }
        //    return pairSmart;
        //}

        public decimal GetFilteredBuyQuantity(BinanceClient client, string symbol, decimal price) {
            decimal retQuantity = 0;
            try {
                BinanceExchangeInfo exchangeInfo = GetExchangeInfo(client);
                if (exchangeInfo != null) {
                    var symbolData = exchangeInfo.Symbols.Where(s => s.SymbolName == symbol).FirstOrDefault();
                    if (symbolData != null) {
                        decimal tradeLimit = Convert.ToDecimal(dbHelper.GetSettingByKey("TradingLimitPerPair"));
                        decimal originalQuantity = tradeLimit / price;
                        BinanceSymbolPriceFilter priceFilter = (BinanceSymbolPriceFilter)symbolData.Filters.Where(f => f.FilterType == SymbolFilterType.PriceFilter).FirstOrDefault();
                        BinanceSymbolLotSizeFilter lotSizeFilter = (BinanceSymbolLotSizeFilter)symbolData.Filters.Where(f => f.FilterType == SymbolFilterType.LotSize).FirstOrDefault();
                        BinanceSymbolMinNotionalFilter minNotionalFilter = (BinanceSymbolMinNotionalFilter)symbolData.Filters.Where(f => f.FilterType == SymbolFilterType.MinNotional).FirstOrDefault();
                        retQuantity = originalQuantity - (originalQuantity % lotSizeFilter.StepSize);
                        retQuantity = Math.Round(retQuantity, 20);
                        var isValidQuantity = (retQuantity >= lotSizeFilter.MinQuantity && retQuantity <= lotSizeFilter.MaxQuantity);
                        if (!isValidQuantity) {
                            return 0; //quantity doesn't match for the min and max criteria for buy/sell
                        } else {
                            decimal totalPrice = retQuantity * price;
                            totalPrice = totalPrice - (totalPrice % priceFilter.TickSize);
                            var isValidMinNotional = (totalPrice >= minNotionalFilter.MinNotional);
                            if (!isValidMinNotional) {
                                return 0; //quantity total price doesn't match for the min notional
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return retQuantity;
        }

        public decimal GetFilteredSellQuantity(BinanceClient client, string symbol, decimal quantity) {
            decimal retQuantity = 0;
            try {
                BinanceExchangeInfo exchangeInfo = GetExchangeInfo(client);
                if (exchangeInfo != null) {
                    var symbolData = exchangeInfo.Symbols.Where(s => s.SymbolName == symbol).FirstOrDefault();
                    if (symbolData != null) {
                        BinanceSymbolLotSizeFilter lotSizeFilter = (BinanceSymbolLotSizeFilter)symbolData.Filters.Where(f => f.FilterType == SymbolFilterType.LotSize).FirstOrDefault();
                        retQuantity = quantity - (quantity % lotSizeFilter.StepSize);
                        retQuantity = Math.Round(retQuantity, 20);
                        var isValidQuantity = (retQuantity >= lotSizeFilter.MinQuantity && retQuantity <= lotSizeFilter.MaxQuantity);
                        if (!isValidQuantity) {
                            return 0;
                        }
                    }
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return retQuantity;
        }
        #endregion

        #region Calcluation Methods
        public decimal GetPercentPrice(decimal price) {
            decimal minProfitPercent = GetMinProfitPercent();
            decimal exchangeTradingFee = GetExchangeTradingFeePercent();
            decimal retVal = price * minProfitPercent / 100;
            if (exchangeTradingFee > 0) {
                retVal = retVal + (price * exchangeTradingFee / 100);
            }
            return Math.Round(retVal, 8);
        }

        public decimal GetBuyPrice(decimal avgWeightedPrice) {
            decimal retValue = avgWeightedPrice - GetPercentPrice(avgWeightedPrice);
            return retValue;
        }

        public decimal GetSellPrice(decimal buyPrice) {
            decimal retValue = buyPrice + GetPercentPrice(buyPrice);
            return retValue;
        }
        #endregion
    }

    public enum Strategy {
        Smart = 1
    }
}
