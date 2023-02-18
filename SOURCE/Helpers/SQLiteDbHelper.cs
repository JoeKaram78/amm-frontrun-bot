using System;
using System.Linq;
using System.Threading.Tasks;
using SqlNado;
using SmartCryptoBot.Models;
using SmartCryptoBot.Libraries;
using System.Collections.Generic;

namespace SmartCryptoBot.Helpers {

    public class SQLiteDbHelper {

        public enum TradeStates {
            Buy = 1,
            Sell = 2,
            Cancelled = 3
        }

        ILogger _logger = new Logger(typeof(SQLiteDbHelper));
        private static readonly string dbName = "db.sqlite";

        /// <summary>
        /// Ask SqlNado to create or synchronize the table with the current object layout
        /// </summary>
        /// <returns></returns>
        public bool IsDbCreatedOrSynced() {
            bool isSynced = false;
            try {
                using (var db = new SQLiteDatabase(dbName)) {
                    db.SynchronizeSchema<Settings>();
                    db.SynchronizeSchema<Trades>();
                    var settingsData = db.LoadAll<Settings>();
                    var allTrades = db.LoadAll<Trades>();
                    isSynced = true;
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return isSynced;
        }

        #region Settings Table
        /// <summary>
        /// Save Setting by key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SaveSettings(string key, string value) {
            try {
                using (var db = new SQLiteDatabase(dbName)) {
                    var setting = new Settings();
                    setting.Key = key;
                    setting.Value = value;
                    db.Save(setting);
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
        }

        /// <summary>
        /// Get Setting by key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetSettingByKey(string key) {
            string value = string.Empty;
            try {
                using (var db = new SQLiteDatabase(dbName)) {
                    var setting = db.LoadByPrimaryKey<Settings>(key);
                    if (setting != null) {
                        value = setting.Value;
                    }
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return value;
        }
        #endregion

        #region Trades Table
        /// <summary>
        /// Create or Save Trade Record
        /// </summary>
        /// <param name="trades"></param>
        public void SaveTrades(Trades trades) {
            try {
                using (var db = new SQLiteDatabase(dbName)) {
                    var trade = new Trades();
                    trade.Id = trades.Id;
                    trade.Symbol = trades.Symbol;
                    trade.Quantity = trades.Quantity;
                    trade.BuyPrice = trades.BuyPrice;
                    trade.BuyQuoteTotalPrice = trades.BuyQuoteTotalPrice;
                    trade.BuyTradeDateTime = trades.BuyTradeDateTime;
                    trade.ExpectedSellPrice = trades.ExpectedSellPrice;
                    trade.SellPrice = trades.SellPrice;
                    trade.SellQuoteTotalPrice = trades.SellQuoteTotalPrice;
                    trade.SellTradeDateTime = trades.SellTradeDateTime;
                    trade.TradeState = trades.TradeState;
                    db.Save(trade);
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
        }

        /// <summary>
        /// Get Trade Status by Status Id
        /// </summary>
        /// <param name="tradeStatusId"></param>
        /// <returns></returns>
        private string GetTradeStatus(int tradeStatusId) {
            string retVal = string.Empty;
            switch (tradeStatusId) {
                case 1: retVal = "Buy"; break;
                case 2: retVal = "Sell"; break;
                case 3: retVal = "Cancelled"; break;
            }
            return retVal;
        }

        /// <summary>
        /// Get Trade Record by Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Trades GetTradeById(string id) {
            Trades trades = new Trades();
            try {
                using (var db = new SQLiteDatabase(dbName)) {
                    trades = db.LoadByPrimaryKey<Trades>(id);
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return trades;
        }

        /// <summary>
        /// Get first open trade record by Symbol
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Trades GetOpenTradeBySymbol(string symbol) {
            Trades trades = new Trades();
            try {
                using (var db = new SQLiteDatabase(dbName)) {
                    trades = db.LoadAll<Trades>().Where(t => t.Symbol == symbol && t.TradeState == (int)TradeStates.Buy).FirstOrDefault();
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return trades;
        }

        /// <summary>
        /// Get all trades
        /// </summary>
        /// <returns></returns>
        public List<Trades> GetTrades() {
            List<Trades> trades = new List<Trades>();
            try {
                using (var db = new SQLiteDatabase(dbName)) {
                    trades = db.LoadAll<Trades>().ToList();
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return trades;
        }

        /// <summary>
        /// Get all open trades
        /// </summary>
        /// <returns></returns>
        public List<Trades> GetOpenTrades() {
            List<Trades> trades = new List<Trades>();
            try {
                using (var db = new SQLiteDatabase(dbName)) {
                    trades = db.LoadAll<Trades>().Where(t => t.TradeState == (int)TradeStates.Buy).ToList();
                }
            } catch (Exception ex) {
                _logger.LogException(ex);
            }
            return trades;
        }
        #endregion
    }

}
