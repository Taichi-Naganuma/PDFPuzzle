using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PDFPuzzle
{
    /// <summary>
    /// チーム版 v0 のローカル席数管理（§3.2）。
    /// %LOCALAPPDATA%\PDFPuzzle\activations\&lt;licenseKeyHash&gt;.json に
    /// 端末（席）の登録状況を保存する。生のライセンスキーはファイル名にも JSON にも保存しない。
    /// </summary>
    public class ActivationStore
    {
        /// <summary>
        /// テスト用: activations 保存先のベースディレクトリを差し替える。
        /// null の場合は %LOCALAPPDATA%\PDFPuzzle\activations を使う。
        /// 単体テストは一時ディレクトリを指定し、実 %LOCALAPPDATA% を汚さないこと。
        /// </summary>
        internal static string? OverrideBaseDir { get; set; }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>1端末（席）の登録情報。</summary>
        public class DeviceRecord
        {
            public string DeviceId { get; set; } = string.Empty;
            public string MachineName { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public DateTime FirstActivatedAt { get; set; } = DateTime.Now;
            public DateTime LastUsedAt { get; set; } = DateTime.Now;
        }

        // --- 永続化されるフィールド ---
        public string LicenseKeyHash { get; set; } = string.Empty;
        public int SeatCount { get; set; } = 3;
        public LicenseTier Tier { get; set; } = LicenseTier.Team;
        public List<DeviceRecord> Devices { get; set; } = new();

        // 保存先パス。Load() でのみ設定される（直接 new した場合は空）。
        [JsonIgnore]
        private string _filePath = string.Empty;

        /// <summary>登録済み端末数（使用中の席数）。</summary>
        [JsonIgnore]
        public int UsedSeats => Devices.Count;

        private static string BaseDir =>
            OverrideBaseDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PDFPuzzle", "activations");

        /// <summary>
        /// ライセンスキーの SHA-256 先頭16文字（小文字 hex）。ファイル名・JSON キー識別子に使う。
        /// </summary>
        public static string HashLicenseKey(string licenseKey)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(licenseKey ?? string.Empty));
            return Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant();
        }

        /// <summary>
        /// ライセンスキーに対応する store を読み込む（席数を権威更新しない版）。
        /// ファイルに記録済みの <see cref="SeatCount"/> をそのまま読む。
        /// 席返却（<c>ReleaseSeat</c> 等）のように席数の権威更新を要さない呼び出し用。
        /// 対応ファイルが無い／読込失敗時は新規 store（フェイルセーフ 3 席）として扱う。
        /// </summary>
        public static ActivationStore Load(string licenseKey)
            => LoadInternal(licenseKey, authoritativeSeatCount: null, fallbackSeatCount: 3);

        /// <summary>
        /// ライセンスキーに対応する store を読み込み、<paramref name="seatCount"/> を
        /// store の権威値として採用する（v0.2 ── verify レスポンス由来の席数を反映）。
        /// 既存ファイルに記録された <see cref="SeatCount"/> と異なる場合は
        /// <paramref name="seatCount"/> で上書きする（ライセンスのアップグレード反映）。
        /// 対応ファイルが無い／読込失敗時は新規 store（<paramref name="seatCount"/> で初期化）として扱う。
        /// </summary>
        /// <param name="licenseKey">verify に成功したライセンスキー（生キー）。</param>
        /// <param name="seatCount">verify レスポンスから判定した席数（権威値）。</param>
        public static ActivationStore Load(string licenseKey, int seatCount)
            => LoadInternal(licenseKey, authoritativeSeatCount: seatCount, fallbackSeatCount: seatCount);

        /// <summary>
        /// <see cref="Load(string)"/> / <see cref="Load(string,int)"/> の共通処理。
        /// <paramref name="authoritativeSeatCount"/> が非 null のとき、読み込んだ store の
        /// <see cref="SeatCount"/> をその値で上書きする（席数の権威化）。
        /// </summary>
        private static ActivationStore LoadInternal(
            string licenseKey, int? authoritativeSeatCount, int fallbackSeatCount)
        {
            string hash = HashLicenseKey(licenseKey);
            string filePath = Path.Combine(BaseDir, $"{hash}.json");

            ActivationStore store;
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    store = JsonSerializer.Deserialize<ActivationStore>(json, JsonOptions)
                            ?? NewStore(hash, fallbackSeatCount);
                }
                else
                {
                    store = NewStore(hash, fallbackSeatCount);
                }
            }
            catch
            {
                // 読込失敗は新規 store として扱う（AppSettings.Load() と同じ堅牢性）。
                store = NewStore(hash, fallbackSeatCount);
            }

            // 識別子の整合性を担保（破損ファイル対策）。
            store.LicenseKeyHash = hash;
            store._filePath = filePath;

            // 席数の権威化: verify 由来の値が渡された場合、ファイル記録値と異なれば上書きする
            // （ライセンスのアップグレード = 3席→5席 を反映）。
            // ダウングレード（席数縮小）の超過端末強制解除は第1次の範囲外（値の更新のみ）。
            if (authoritativeSeatCount.HasValue)
                store.SeatCount = authoritativeSeatCount.Value;

            return store;
        }

        private static ActivationStore NewStore(string hash, int seatCount) => new()
        {
            LicenseKeyHash = hash,
            SeatCount = seatCount,
            Tier = LicenseTier.Team,
            Devices = new List<DeviceRecord>()
        };

        /// <summary>指定端末が登録済みか。</summary>
        public bool ContainsDevice(string deviceId)
            => Devices.Any(d => string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));

        private DeviceRecord? Find(string deviceId)
            => Devices.FirstOrDefault(d => string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// 端末を登録する。
        /// - 既存端末: lastUsedAt を更新し true（席は消費しない）。
        /// - 空き席あり: 登録して true。
        /// - 満席かつ未登録: false。
        /// </summary>
        public bool TryAddDevice(string deviceId, string machineName, string userName)
        {
            var existing = Find(deviceId);
            if (existing != null)
            {
                existing.LastUsedAt = DateTime.Now;
                return true;
            }

            if (UsedSeats >= SeatCount)
                return false;

            var now = DateTime.Now;
            Devices.Add(new DeviceRecord
            {
                DeviceId = deviceId,
                MachineName = machineName,
                UserName = userName,
                FirstActivatedAt = now,
                LastUsedAt = now
            });
            return true;
        }

        /// <summary>既存端末の lastUsedAt を更新する。未登録なら何もしない。</summary>
        public void TouchLastUsed(string deviceId)
        {
            var existing = Find(deviceId);
            if (existing != null)
                existing.LastUsedAt = DateTime.Now;
        }

        /// <summary>端末を削除して席を返却する。削除できれば true。</summary>
        public bool RemoveDevice(string deviceId)
        {
            var existing = Find(deviceId);
            if (existing == null)
                return false;
            Devices.Remove(existing);
            return true;
        }

        /// <summary>store を JSON として保存する。</summary>
        public void Save()
        {
            try
            {
                string filePath = string.IsNullOrEmpty(_filePath)
                    ? Path.Combine(BaseDir, $"{LicenseKeyHash}.json")
                    : _filePath;
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                string json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(filePath, json);
                _filePath = filePath;
            }
            catch { }
        }
    }
}
