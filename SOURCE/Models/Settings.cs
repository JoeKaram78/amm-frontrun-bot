using SqlNado;

namespace SmartCryptoBot.Models {

    [SQLiteTable]
    public class Settings {

        [SQLiteColumn(IsPrimaryKey = true)]
        public string Key { get; set; }

        public string Value { get; set; }

    }
}
