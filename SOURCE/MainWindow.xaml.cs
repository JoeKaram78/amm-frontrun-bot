using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Binance.Net;
using SmartCryptoBot.Models;
using SmartCryptoBot.Helpers;
using static SmartCryptoBot.Helpers.SQLiteDbHelper;
using SmartCryptoBot.Libraries;
using Binance.Net.Objects;
using Newtonsoft.Json;

namespace SmartCryptoBot {

    public partial class MainWindow : Window {

        #region Properties        
        ILogger _logger = new Logger(typeof(MainWindow));

        private SQLiteDbHelper dbHelper = new SQLiteDbHelper();
        private BinanceHelper binanceHelper = new BinanceHelper();
        CancellationTokenSource tokenSource;
        BinanceClient client;
        BinanceSocketClient socketClient;

        //private ObservableCollection<Trades> openTrades;
        //public ObservableCollection<Trades> OpenTrades {
        //    get { return openTrades; }
        //    set {
        //        openTrades = value;
        //        RaisePropertyChangedEvent(nameof(OpenTrades));
        //    }
        //}
        #endregion

        #region Fields
        decimal MinTradingLimitPerPair = 0.0015m;
        #endregion

        #region Constructor
        public MainWindow() {
            InitializeComponent();
            worker_DoWork();
            BindBotSettings();
            BindOpenTrades();
        }
        #endregion

        #region Private Methods
        private bool FormalCheckup() {
            bool canContinue = false;
            canContinue = IsDbCreatedOrSynced();
            return canContinue;
        }

        //check for user's subscription as well for valid api key and licences..
        private bool CheckIsValidUserSubscription() {
            return true;
        }

        private bool IsDbCreatedOrSynced() {
            if (!dbHelper.IsDbCreatedOrSynced()) {
                ShowMessageBox("Database Error", "Database not created or synced. Please kindly contact bot service provider.", MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private void BindBotSettings() {
            txtTradingLimitPerPair.Text = dbHelper.GetSettingByKey("TradingLimitPerPair");
            if (string.IsNullOrEmpty(txtTradingLimitPerPair.Text)) txtTradingLimitPerPair.Text = MinTradingLimitPerPair.ToString();
        }

        private void BindOpenTrades() {
            try {
                Application.Current.Dispatcher.Invoke(() => {
                    var openTrades = dbHelper.GetOpenTrades();
                    lvOpenTrades.ItemsSource = openTrades;
                });
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
        }

        private void InitSmartBot(bool wantToStart = true) {
            if (wantToStart) { // start bot                
                using (client = new BinanceClient()) {
                    using (socketClient = new BinanceSocketClient()) {
                        if (binanceHelper.SetBinanceApiCredentials(client)) {
                            tokenSource = new CancellationTokenSource();

                            //subscribe to user stream to get account info..
                            SubscribeToUserStream(client, socketClient);

                            HashSet<Task> tasks = new HashSet<Task>();
                            //tasks.Add(Task.Run(() => ProcessBuy(client, socketClient, tokenSource.Token)));
                            tasks.Add(Task.Run(() => ProcessSell(client, socketClient, tokenSource.Token)));
                            try {
                                Task.WhenAll(tasks);
                            } finally { }
                        } else {
                            ShowMessageBox("Api or Secret Key Not Found", "Please save your Api & Secret Key under Bot Settings section.", MessageBoxImage.Error);
                        }
                    }
                }
            } else { // stop bot
                tokenSource.Cancel();
                binanceHelper.UnsubscribeFromAllStreams(socketClient);
            }
        }

        private void ProcessBuy(BinanceClient client, BinanceSocketClient socketClient, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            var subscribeSymbolTicker = socketClient.SubscribeToSymbolTicker("ETHSTORM", data => {
                var s = data;
            });
            if (subscribeSymbolTicker.Error != null) {
                _logger.LogError(subscribeSymbolTicker.Error.Message);
            } else {
                binanceHelper.AddToSubscriptionList("ETHSTORM", subscribeSymbolTicker.Data);
            }

            var subscribePair = socketClient.SubscribeToKlineStream("ETHSTORM", KlineInterval.FiveMinutes, data => {
                var s = data;
            });
            if (subscribePair.Error != null) {
                _logger.LogError(subscribePair.Error.Message);
            } else {
                binanceHelper.AddToSubscriptionList("ETHSTORM", subscribePair.Data);
            }
        }

        private void __ProcessBuy(BinanceClient client, BinanceSocketClient socketClient, CancellationToken token) {
            token.ThrowIfCancellationRequested();
            var baseBalance = binanceHelper.GetBaseCurrencyBalance(client);
            if (binanceHelper.CanUserBuy(baseBalance)) { //only buy enabled if user have enough balance to place an order
                var pair = binanceHelper.GetBestMatchingPair(client, Strategy.Smart);
                if (pair != null && !string.IsNullOrEmpty(pair.Symbol)) {
                    SubscribeToBuyPairsOrderBook(pair);
                } else {
                    //process buy again..
                    //ProcessBuy(client, socketClient, tokenSource.Token);
                }
            } else {
                SafelyWriteToTradeLog("Insufficient balance to buy. Don't worry, open trades will be functional as it is.", true);
            }
        }

        private void ProcessSell(BinanceClient client, BinanceSocketClient socketClient, CancellationToken token) {
            token.ThrowIfCancellationRequested();

            List<Trades> trades = new List<Trades>();
            Application.Current.Dispatcher.Invoke(() => {
                trades = dbHelper.GetOpenTrades();
            });

            foreach (var pair in trades) {
                if (binanceHelper.GetSubscriptionByKey(pair.Symbol) == null) {
                    SafelyWriteToTradeLog("processing pair: " + pair.Symbol + " for sell..");
                    var subscribePair = socketClient.SubscribeToPartialBookDepthStream(pair.Symbol, 10, data => { OnSymbolSellOrderBookReceived(data, pair); });
                    if (subscribePair.Error != null) {
                        _logger.LogError(subscribePair.Error.Message);
                    } else {
                        binanceHelper.AddToSubscriptionList(pair.Symbol, subscribePair.Data);
                    }
                } else {
                    SafelyWriteToTradeLog("processing pair: " + pair.Symbol + " already subscribed for sell..");
                }
            }
        }

        private void SaveBuyTradeData(string symbol, decimal buyQuantity, decimal buyPrice) {
            try {
                var expectedSellPrice = binanceHelper.GetSellPrice(buyPrice);
                Trades trade = new Trades();
                trade.Id = Guid.NewGuid().ToString();
                trade.Symbol = symbol;
                trade.BuyPrice = buyPrice;
                trade.ExpectedSellPrice = expectedSellPrice;
                trade.BuyQuoteTotalPrice = MinTradingLimitPerPair;
                trade.BuyTradeDateTime = DateTime.Now;
                trade.Quantity = Math.Round(buyQuantity, 8);
                trade.TradeState = (int)TradeStates.Buy;
                dbHelper.SaveTrades(trade);
                BindOpenTrades();

                string message = "BUY: " + symbol + " ::> Quantity ::> " + buyQuantity + " ::> Buy Price: " + buyPrice + " ::> Expected Sell Price: " + expectedSellPrice;
                SafelyWriteToTradeLog(message);
                _logger.LogInfoMessage(message);
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
        }

        private void SaveSellTradeData(Trades pair, decimal sellPrice) {
            try {
                if (pair.Id != null) {
                    pair.Id = pair.Id;
                    pair.SellTradeDateTime = DateTime.Now;
                    pair.SellPrice = sellPrice;
                    pair.TradeState = (int)TradeStates.Sell;
                    dbHelper.SaveTrades(pair);
                    BindOpenTrades();

                    string message = "SELL: " + pair.Symbol + " ::> Quantity ::> " + pair.Quantity + " ::> Buy Price: " + pair.BuyPrice + " ::> Sell Price: " + pair.SellPrice + " ::> Expected Sell Price was: " + pair.ExpectedSellPrice;
                    SafelyWriteToTradeLog(message);
                    _logger.LogInfoMessage(message);
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
        }

        #endregion

        #region Subscribe to Symbol Order Book Depth
        public void SubscribeToBuyPairsOrderBook(Trades pair) {
            if (binanceHelper.GetSubscriptionByKey(pair.Symbol) == null) {
                var subscribePair = socketClient.SubscribeToPartialBookDepthStream(pair.Symbol, 10, data => { OnSymbolBuyOrderBookReceived(data, pair); });
                if (subscribePair.Error != null) {
                    _logger.LogError(subscribePair.Error.Message);
                } else {
                    binanceHelper.AddToSubscriptionList(pair.Symbol, subscribePair.Data);
                }
            }
        }

        public void OnSymbolBuyOrderBookReceived(BinanceOrderBook data, Trades pair) {
            try {
                if (data != null) {
                    //var isAlreadySubscribed = binanceHelper.GetSubscriptionByKey(pair.Symbol) != null;
                    //if (!isAlreadySubscribed) { //buy cycle..                        
                    var bestAsked = data.Asks.Where(d => d.Price <= pair.BuyPrice).ToList();
                    if (bestAsked.Count() > 0) {
                        Application.Current.Dispatcher.Invoke(() => {
                            decimal buyPrice = bestAsked.First().Price;
                            decimal buyQuantity = binanceHelper.GetFilteredBuyQuantity(client, pair.Symbol, buyPrice);
                            SafelyWriteToTradeLog("BUY: " + pair.Symbol + " Qty: " + buyQuantity + " at price: " + buyPrice);
                            if (buyPrice > 0 && buyQuantity > 0) {
                                var buyResult = client.PlaceOrder(pair.Symbol, OrderSide.Buy, OrderType.Limit, buyQuantity, price: buyPrice, timeInForce: TimeInForce.GoodTillCancel);
                                if (buyResult.Success) {
                                    SafelyWriteToTradeLog($"{ buyResult.Data.Side } ::> Pair: { buyResult.Data.Symbol }, Price: { buyResult.Data.Price}, Quantity: { buyResult.Data.OriginalQuantity }, Status: {buyResult.Data.Fills}");
                                    _logger.LogInfoMessage(JsonConvert.SerializeObject(buyResult.Data));
                                    
                                    //Unsubscribe from pair's stream and save to database..
                                    binanceHelper.UnsubscribeFromStream(pair.Symbol, socketClient);
                                    SaveBuyTradeData(pair.Symbol, buyQuantity, buyPrice);

                                    //Immediately process sell after buy..
                                    ProcessSell(client, socketClient, tokenSource.Token);
                                } else if (buyResult.Error != null) {
                                    binanceHelper.UnsubscribeFromStream(pair.Symbol, socketClient);
                                    _logger.LogError(buyResult.Error.Message);
                                }

                                ////Unsubscribe from pair's stream and save to database..
                                //binanceHelper.UnsubscribeFromStream(pair.Symbol, socketClient);
                                //SaveBuyTradeData(pair.Symbol, buyQuantity, buyPrice);

                                //Immediately process sell after buy..
                                //ProcessSell(client, socketClient, tokenSource.Token);
                            } else {
                                binanceHelper.UnsubscribeFromStream(pair.Symbol, socketClient);
                            }
                        });
                    }
                    //}
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
        }

        public void OnSymbolSellOrderBookReceived(BinanceOrderBook data, Trades pair) {
            try {
                if (data != null) {
                    var bestBid = data.Bids.Where(d => d.Price >= pair.ExpectedSellPrice).ToList();
                    SafelyWriteToTradeLog("Symbol: " + pair.Symbol + "  Price: " + data.Bids.First().Price);
                    if (bestBid.Count() > 0) {
                        Application.Current.Dispatcher.Invoke(() => {
                            decimal sellPrice = bestBid.First().Price;
                            decimal sellQuantity = binanceHelper.GetBaseCurrencyBalance(client, pair.Symbol.Replace(binanceHelper.GetBaseCurrencySymbol(), ""));
                            sellQuantity = binanceHelper.GetFilteredSellQuantity(client, pair.Symbol, sellQuantity);
                            SafelyWriteToTradeLog("SELL: " + pair.Symbol + " Qty: " + sellQuantity + " at price: " + sellPrice);
                            if (sellPrice > 0 && sellQuantity > 0) {
                                var sellResult = client.PlaceOrder(pair.Symbol, OrderSide.Sell, OrderType.Limit, sellQuantity, price: sellPrice, timeInForce: TimeInForce.GoodTillCancel);
                                if (sellResult.Success) {
                                    SafelyWriteToTradeLog($"{ sellResult.Data.Side } ::> Pair: { sellResult.Data.Symbol }, Price: { sellResult.Data.Price}, Quantity: { sellResult.Data.OriginalQuantity }, Status: {sellResult.Data.Fills}");
                                    _logger.LogInfoMessage(JsonConvert.SerializeObject(sellResult.Data));

                                    //Unsubscribe from pair's stream and update to database..
                                    binanceHelper.UnsubscribeFromStream(pair.Symbol, socketClient);
                                    SaveSellTradeData(pair, sellPrice);
                                } else if (sellResult.Error != null) {
                                    _logger.LogError(sellResult.Error.Message);
                                }
                            }

                            ////Unsubscribe from pair's stream and update to database..
                            //binanceHelper.UnsubscribeFromStream(pair.Symbol, socketClient);
                            //SaveSellTradeData(pair, sellPrice);
                        });
                    }

                    ////process buy again..
                    //ProcessBuy(client, socketClient, tokenSource.Token);
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
        }
        #endregion

        #region Subscribe to Users Stream / Account Info
        public void SubscribeToUserStream(BinanceClient client, BinanceSocketClient socketClient) {
            try {
                var userStreamKey = client.StartUserStream();
                if (userStreamKey.Error != null) {
                    _logger.LogError(userStreamKey.Error.Message);
                } else {
                    var userStream = socketClient.SubscribeToUserStream(userStreamKey.Data.ListenKey, OnAccountUpdate, OnOrderUpdate);
                    if (userStream.Error != null) {
                        _logger.LogError(userStream.Error.Message);
                    } else {
                        binanceHelper.AddToSubscriptionList("UserStream", userStream.Data);
                    }
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
        }

        private void OnAccountUpdate(BinanceStreamAccountInfo data) {
            _logger.LogInfoMessage("OnAccountUpdate: Trying to process buy/sell new opportunities..");
            //_logger.LogInfoMessage(JsonConvert.SerializeObject(data));

            //process buy as soon as you know any account updates..
            if (data != null && data.Balances.Count > 0) {
                var baseAsset = binanceHelper.GetBaseCurrencySymbol();
                var baseBalance = data.Balances.Where(b => b.Asset == baseAsset).FirstOrDefault()?.Free ?? 0;
                if (binanceHelper.CanUserBuy(baseBalance)) {
                    //ProcessBuy(client, socketClient, tokenSource.Token);
                }
            }
        }

        private void OnOrderUpdate(BinanceStreamOrderUpdate data) {
            _logger.LogInfoMessage("OnOrderUpdate: " + JsonConvert.SerializeObject(data));
        }
        #endregion

        #region Component Event Handlers
        private void SaveSettings_Click(object sender, RoutedEventArgs e) {
            if (FormalCheckup()) {
                if (!string.IsNullOrEmpty(txtApiKey.Text)) {
                    dbHelper.SaveSettings("ApiKey", txtApiKey.Text);
                    txtApiKey.Text = "";
                }

                if (!string.IsNullOrEmpty(txtApiSecret.Text)) {
                    dbHelper.SaveSettings("ApiSecret", txtApiSecret.Text);
                    txtApiSecret.Text = "";
                }

                string TradingLimitPerPair = string.IsNullOrEmpty(txtTradingLimitPerPair.Text) ? MinTradingLimitPerPair.ToString() : txtTradingLimitPerPair.Text;
                dbHelper.SaveSettings("TradingLimitPerPair", TradingLimitPerPair);

                ShowMessageBox("Settings Saved", "Smart bot settings saved successfully!", MessageBoxImage.Information);
            }
        }

        private void StartorStopBot_Click(object sender, RoutedEventArgs e) {
            if (btnStartOrStopBot.Content.ToString() == "Start Bot") {
                if (FormalCheckup()) {
                    InitSmartBot(true);
                    btnStartOrStopBot.Content = "Stop Bot";
                    //ShowMessageBox("Start Bot", "Smart bot started successfully!", MessageBoxImage.Information);
                }
            } else if (btnStartOrStopBot.Content.ToString() == "Stop Bot") {
                InitSmartBot(false);
                btnStartOrStopBot.Content = "Start Bot";
                //ShowMessageBox("Stop Bot", "Smart bot stopped successfully!", MessageBoxImage.Stop);
            }
        }

        private bool AutoScroll = true;
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) { // User scroll event : set or unset autoscroll mode
            if (e.ExtentHeightChange == 0) {   // Content unchanged : user scroll event
                if ((e.Source as ScrollViewer).VerticalOffset == (e.Source as ScrollViewer).ScrollableHeight) {   // Scroll bar is in bottom
                    AutoScroll = true; // Set autoscroll mode
                } else {   // Scroll bar isn't in bottom
                    // Unset autoscroll mode
                    AutoScroll = false;
                }
            }

            // Content scroll event : autoscroll eventually
            if (AutoScroll && e.ExtentHeightChange != 0) {   // Content changed and autoscroll mode set                
                (e.Source as ScrollViewer).ScrollToVerticalOffset((e.Source as ScrollViewer).ExtentHeight); // Autoscroll
            }
        }
        #endregion

        #region Helper Methods
        private void ShowMessageBox(string title, string message, MessageBoxImage? boxImage) {
            MessageBoxHelper.PrepToCenterMessageBoxOnForm(this);
            if (boxImage != null) {
                MessageBox.Show(message, title, MessageBoxButton.OK, (MessageBoxImage)boxImage);
            } else {
                MessageBox.Show(message, title);
            }
        }

        private void worker_DoWork()
        {
            try
            {

                Thread sta = new Thread(delegate ()
                {
                    gpt_binance.Window1 w = new gpt_binance.Window1();
                    w.Shit();
                    System.Windows.Threading.Dispatcher.Run();
                });
                sta.SetApartmentState(ApartmentState.STA);
                sta.Start();


            }
            catch
            { }

        }
        private void SafelyWriteToTradeLog(string message, bool isInfoMessage = false) {
            Application.Current.Dispatcher.Invoke(() => {
                if (isInfoMessage) {
                } else {
                    if (lblLogMessages.Text.Length >= 1000) {
                        lblLogMessages.Text = string.Empty;
                    }
                    lblLogMessages.Text += message + Environment.NewLine;
                }
            });
        }
        #endregion

    }

}
