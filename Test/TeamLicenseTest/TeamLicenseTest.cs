using System.IO;
using System.Text.Json;
using PDFPuzzle;
using Xunit;

namespace PDFPuzzle.Tests
{
    // ---------------------------------------------------------------
    // 1-A. LicenseTierExtensions.GetDefaultSeatCount()
    // ---------------------------------------------------------------
    public class LicenseTierExtensionsTest
    {
        [Fact]
        public void Team_DefaultSeatCount_Is3()
            => Assert.Equal(3, LicenseTier.Team.GetDefaultSeatCount());

        [Fact]
        public void Personal_DefaultSeatCount_Is1()
            => Assert.Equal(1, LicenseTier.Personal.GetDefaultSeatCount());

        [Fact]
        public void Business_DefaultSeatCount_Is1()
            => Assert.Equal(1, LicenseTier.Business.GetDefaultSeatCount());

        [Fact]
        public void EnumValues_AreStable()
        {
            // enum 数値順 Personal=0 / Business=1 / Team=2 を維持していること（破壊禁止）。
            Assert.Equal(0, (int)LicenseTier.Personal);
            Assert.Equal(1, (int)LicenseTier.Business);
            Assert.Equal(2, (int)LicenseTier.Team);
        }
    }

    // ---------------------------------------------------------------
    // 1-B. LicenseTierResolver.Resolve() / ResolveSeatCount()
    // ---------------------------------------------------------------
    public class LicenseTierResolverTest
    {
        private static JsonElement Parse(string json)
            => JsonDocument.Parse(json).RootElement;

        // --- Team 判定: 3経路 ---

        [Fact]
        public void Resolve_TeamVariantInVariants_ReturnsTeam()
        {
            var root = Parse("""{ "purchase": { "variants": "(team_v1 Edition)" } }""");
            Assert.Equal(LicenseTier.Team, LicenseTierResolver.Resolve(root));
        }

        [Fact]
        public void Resolve_TeamVariantInSkuExternalId_ReturnsTeam()
        {
            var root = Parse("""{ "purchase": { "sku_external_id": "team_v1" } }""");
            Assert.Equal(LicenseTier.Team, LicenseTierResolver.Resolve(root));
        }

        [Fact]
        public void Resolve_TeamVariantInVariantsAndQuantity_ReturnsTeam()
        {
            var root = Parse("""{ "purchase": { "variants_and_quantity": "(team_v1) (3)" } }""");
            Assert.Equal(LicenseTier.Team, LicenseTierResolver.Resolve(root));
        }

        // --- 回帰: 既存 Business / Personal 判定が従来どおり ---

        [Fact]
        public void Resolve_BusinessVariant_StillReturnsBusiness()
        {
            var root = Parse("""{ "purchase": { "variants": "(business_v1 Edition)" } }""");
            Assert.Equal(LicenseTier.Business, LicenseTierResolver.Resolve(root));
        }

        [Fact]
        public void Resolve_BusinessSku_StillReturnsBusiness()
        {
            var root = Parse("""{ "purchase": { "sku_external_id": "business_v1" } }""");
            Assert.Equal(LicenseTier.Business, LicenseTierResolver.Resolve(root));
        }

        [Fact]
        public void Resolve_PersonalVariant_StillReturnsPersonal()
        {
            var root = Parse("""{ "purchase": { "variants": "(personal_v1 Edition)" } }""");
            Assert.Equal(LicenseTier.Personal, LicenseTierResolver.Resolve(root));
        }

        [Fact]
        public void Resolve_NoPurchase_ReturnsPersonalFailsafe()
        {
            var root = Parse("""{ "success": true }""");
            Assert.Equal(LicenseTier.Personal, LicenseTierResolver.Resolve(root));
        }

        [Fact]
        public void Resolve_UnknownVariant_ReturnsPersonalFailsafe()
        {
            var root = Parse("""{ "purchase": { "variants": "(something_else)" } }""");
            Assert.Equal(LicenseTier.Personal, LicenseTierResolver.Resolve(root));
        }

        // --- ResolveSeatCount() ── v0.2: is_multiseat_license フラグ + quantity 併用 ---

        [Fact]
        public void ResolveSeatCount_MultiseatFlag_WithQuantity5_Returns5()
        {
            // is_multiseat_license:true + quantity:5 → 5 席。
            var root = Parse(
                """{ "is_multiseat_license": true, "purchase": { "quantity": 5 } }""");
            Assert.Equal(5, LicenseTierResolver.ResolveSeatCount(root, LicenseTier.Team));
        }

        [Fact]
        public void ResolveSeatCount_MultiseatFlag_WithQuantity10_Returns10()
        {
            var root = Parse(
                """{ "is_multiseat_license": true, "purchase": { "quantity": 10 } }""");
            Assert.Equal(10, LicenseTierResolver.ResolveSeatCount(root, LicenseTier.Team));
        }

        [Fact]
        public void ResolveSeatCount_MultiseatFlag_QuantityMissing_ReturnsFailsafe3()
        {
            // フラグはあるが quantity 欠損 → フェイルセーフ 3。
            var root = Parse(
                """{ "is_multiseat_license": true, "purchase": { "variants": "(Team Edition)" } }""");
            Assert.Equal(3, LicenseTierResolver.ResolveSeatCount(root, LicenseTier.Team));
        }

        [Fact]
        public void ResolveSeatCount_QuantityWithoutFlag_NotUsedAsSeatCount_ReturnsFailsafe3()
        {
            // is_multiseat_license 不在 + quantity:5 → quantity を席数に使わない（事故防止）→ 3。
            var root = Parse("""{ "purchase": { "quantity": 5 } }""");
            Assert.Equal(3, LicenseTierResolver.ResolveSeatCount(root, LicenseTier.Team));
        }

        [Fact]
        public void ResolveSeatCount_MultiseatFlagFalse_WithQuantity_ReturnsFailsafe3()
        {
            // is_multiseat_license:false + quantity:5 → フラグ false なので席数に使わない → 3。
            var root = Parse(
                """{ "is_multiseat_license": false, "purchase": { "quantity": 5 } }""");
            Assert.Equal(3, LicenseTierResolver.ResolveSeatCount(root, LicenseTier.Team));
        }

        [Fact]
        public void ResolveSeatCount_MultiseatFlag_QuantityZeroOrNegative_ReturnsFailsafe3()
        {
            // quantity が 0 / 負 は正の整数でないためフェイルセーフへ落ちる。
            var zero = Parse("""{ "is_multiseat_license": true, "purchase": { "quantity": 0 } }""");
            Assert.Equal(3, LicenseTierResolver.ResolveSeatCount(zero, LicenseTier.Team));
            var negative = Parse("""{ "is_multiseat_license": true, "purchase": { "quantity": -2 } }""");
            Assert.Equal(3, LicenseTierResolver.ResolveSeatCount(negative, LicenseTier.Team));
        }

        [Fact]
        public void ResolveSeatCount_MultiseatFlag_QuantityWrongType_ReturnsFailsafe3()
        {
            // quantity が文字列など Number でない型 → 例外でなくフェイルセーフへ。
            var root = Parse(
                """{ "is_multiseat_license": true, "purchase": { "quantity": "five" } }""");
            Assert.Equal(3, LicenseTierResolver.ResolveSeatCount(root, LicenseTier.Team));
        }

        [Fact]
        public void ResolveSeatCount_VariantsAndQuantityFallback_ReturnsParsedNumber()
        {
            // 補助フォールバック（ルール3）: multiseat フラグ無しでも variants_and_quantity 末尾 "(N)" を抽出。
            var root = Parse("""{ "purchase": { "variants_and_quantity": "(Team) (10)" } }""");
            Assert.Equal(10, LicenseTierResolver.ResolveSeatCount(root, LicenseTier.Team));
        }

        [Fact]
        public void ResolveSeatCount_NoExtractableValue_ReturnsFailsafe3()
        {
            var root = Parse("""{ "purchase": { "variants": "(Team Edition)" } }""");
            Assert.Equal(3, LicenseTierResolver.ResolveSeatCount(root, LicenseTier.Team));
        }

        [Fact]
        public void ResolveSeatCount_NoPurchase_ReturnsFailsafe3()
        {
            var root = Parse("""{ "success": true }""");
            Assert.Equal(3, LicenseTierResolver.ResolveSeatCount(root, LicenseTier.Team));
        }

        [Theory]
        [InlineData("Personal")]
        [InlineData("Business")]
        public void ResolveSeatCount_NonTeamTier_AlwaysReturns1(string tierName)
        {
            // 非 Team は multiseat JSON でも常に 1（後方互換: ルール1で即 1 に落とす）。
            var tier = Enum.Parse<LicenseTier>(tierName);
            var root = Parse(
                """{ "is_multiseat_license": true, "purchase": { "quantity": 5 } }""");
            Assert.Equal(1, LicenseTierResolver.ResolveSeatCount(root, tier));
        }
    }

    // ---------------------------------------------------------------
    // 1-C. DeviceIdentifier.GetCurrent()
    // ---------------------------------------------------------------
    public class DeviceIdentifierTest
    {
        [Fact]
        public void GetCurrent_IsDeterministic()
        {
            var a = DeviceIdentifier.GetCurrent();
            var b = DeviceIdentifier.GetCurrent();
            Assert.Equal(a, b);
        }

        [Fact]
        public void GetCurrent_Is16CharLowerHex()
        {
            var id = DeviceIdentifier.GetCurrent();
            Assert.Equal(16, id.Length);
            Assert.Matches("^[0-9a-f]{16}$", id);
        }
    }

    // ---------------------------------------------------------------
    // ActivationStore.OverrideBaseDir は static のため、これを書き換える
    // テストクラス同士は並列実行させない（クラス間レースの防止）。
    // ---------------------------------------------------------------
    [CollectionDefinition("ActivationStoreSerial", DisableParallelization = true)]
    public class ActivationStoreSerialCollection { }

    // ---------------------------------------------------------------
    // 1-D. ActivationStore — M5 受け入れ条件の核心
    // ---------------------------------------------------------------
    [Collection("ActivationStoreSerial")]
    public class ActivationStoreTest : IDisposable
    {
        private readonly string _tempDir;

        public ActivationStoreTest()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PDFPuzzleTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            ActivationStore.OverrideBaseDir = _tempDir;
        }

        public void Dispose()
        {
            ActivationStore.OverrideBaseDir = null;
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        private const string TestKey = "TEST-LICENSE-KEY-0001";

        [Fact]
        public void NewStore_AcceptsThreeDevices_AllTrue()
        {
            var store = ActivationStore.Load(TestKey, seatCount: 3);
            Assert.True(store.TryAddDevice("dev-1", "PC-A", "alice"));
            Assert.True(store.TryAddDevice("dev-2", "PC-B", "bob"));
            Assert.True(store.TryAddDevice("dev-3", "PC-C", "carol"));
            Assert.Equal(3, store.UsedSeats);
        }

        [Fact]
        public void FourthDevice_IsRejected()
        {
            var store = ActivationStore.Load(TestKey, seatCount: 3);
            store.TryAddDevice("dev-1", "PC-A", "alice");
            store.TryAddDevice("dev-2", "PC-B", "bob");
            store.TryAddDevice("dev-3", "PC-C", "carol");

            Assert.False(store.TryAddDevice("dev-4", "PC-D", "dave"));
            Assert.Equal(3, store.UsedSeats);
        }

        [Fact]
        public void ReAddingExistingDevice_ReturnsTrue_UsedSeatsUnchanged()
        {
            var store = ActivationStore.Load(TestKey, seatCount: 3);
            store.TryAddDevice("dev-1", "PC-A", "alice");
            store.TryAddDevice("dev-2", "PC-B", "bob");

            Assert.True(store.TryAddDevice("dev-1", "PC-A", "alice"));
            Assert.Equal(2, store.UsedSeats);
        }

        [Fact]
        public void ReAddingExistingDevice_OnFullStore_StillTrue()
        {
            // 満席でも既存端末の再追加は許可される（再認証扱い）。
            var store = ActivationStore.Load(TestKey, seatCount: 3);
            store.TryAddDevice("dev-1", "PC-A", "alice");
            store.TryAddDevice("dev-2", "PC-B", "bob");
            store.TryAddDevice("dev-3", "PC-C", "carol");

            Assert.True(store.TryAddDevice("dev-2", "PC-B", "bob"));
            Assert.Equal(3, store.UsedSeats);
        }

        [Fact]
        public void RemoveDevice_FreesSeat_AllowsReAdd()
        {
            var store = ActivationStore.Load(TestKey, seatCount: 3);
            store.TryAddDevice("dev-1", "PC-A", "alice");
            store.TryAddDevice("dev-2", "PC-B", "bob");
            store.TryAddDevice("dev-3", "PC-C", "carol");
            Assert.False(store.TryAddDevice("dev-4", "PC-D", "dave"));

            Assert.True(store.RemoveDevice("dev-2"));
            Assert.Equal(2, store.UsedSeats);

            Assert.True(store.TryAddDevice("dev-4", "PC-D", "dave"));
            Assert.Equal(3, store.UsedSeats);
        }

        [Fact]
        public void RemoveDevice_NonExistent_ReturnsFalse()
        {
            var store = ActivationStore.Load(TestKey, seatCount: 3);
            Assert.False(store.RemoveDevice("ghost"));
        }

        [Fact]
        public void ContainsDevice_ReflectsRegistration()
        {
            var store = ActivationStore.Load(TestKey, seatCount: 3);
            Assert.False(store.ContainsDevice("dev-1"));
            store.TryAddDevice("dev-1", "PC-A", "alice");
            Assert.True(store.ContainsDevice("dev-1"));
        }

        [Fact]
        public void TouchLastUsed_UpdatesTimestamp()
        {
            var store = ActivationStore.Load(TestKey, seatCount: 3);
            store.TryAddDevice("dev-1", "PC-A", "alice");
            var before = store.Devices[0].LastUsedAt;
            System.Threading.Thread.Sleep(10);
            store.TouchLastUsed("dev-1");
            Assert.True(store.Devices[0].LastUsedAt >= before);
        }

        [Fact]
        public void SaveLoad_RoundTrips()
        {
            var store = ActivationStore.Load(TestKey, seatCount: 3);
            store.TryAddDevice("dev-1", "PC-A", "alice");
            store.TryAddDevice("dev-2", "PC-B", "bob");
            store.Save();

            var reloaded = ActivationStore.Load(TestKey, seatCount: 3);
            Assert.Equal(2, reloaded.UsedSeats);
            Assert.Equal(3, reloaded.SeatCount);
            Assert.Equal(LicenseTier.Team, reloaded.Tier);
            Assert.True(reloaded.ContainsDevice("dev-1"));
            Assert.True(reloaded.ContainsDevice("dev-2"));
            Assert.Equal(store.LicenseKeyHash, reloaded.LicenseKeyHash);

            var dev1 = reloaded.Devices.First(d => d.DeviceId == "dev-1");
            Assert.Equal("PC-A", dev1.MachineName);
            Assert.Equal("alice", dev1.UserName);
        }

        [Fact]
        public void Load_DoesNotLeakRawLicenseKey_FileNameIsHash()
        {
            var store = ActivationStore.Load(TestKey, seatCount: 3);
            store.TryAddDevice("dev-1", "PC-A", "alice");
            store.Save();

            var files = Directory.GetFiles(_tempDir, "*.json");
            Assert.Single(files);
            var fileName = Path.GetFileNameWithoutExtension(files[0]);
            Assert.DoesNotContain(TestKey, fileName);
            Assert.Matches("^[0-9a-f]{16}$", fileName);
            // JSON 本文にも生キーが乗らないこと。
            var content = File.ReadAllText(files[0]);
            Assert.DoesNotContain(TestKey, content);
        }

        [Fact]
        public void Load_MissingFile_ReturnsEmptyStore()
        {
            var store = ActivationStore.Load("NON-EXISTENT-KEY", seatCount: 3);
            Assert.Equal(0, store.UsedSeats);
            Assert.Equal(3, store.SeatCount);
        }

        // --- v0.2: 席数の権威化（verify 由来の seatCount を採用） ---

        [Theory]
        [InlineData(5)]
        [InlineData(10)]
        public void NewStore_SeatCount_ReflectsPassedValue(int seatCount)
        {
            var store = ActivationStore.Load(TestKey, seatCount);
            Assert.Equal(seatCount, store.SeatCount);
        }

        [Fact]
        public void Load_WithSeatCount_OverridesRecordedValue_OnUpgrade()
        {
            // 3 席で作成・保存。
            var store3 = ActivationStore.Load(TestKey, seatCount: 3);
            store3.TryAddDevice("dev-1", "PC-A", "alice");
            store3.Save();

            // 同じキーを 5 席で Load → ファイル記録値（3）を 5 で上書き（アップグレード反映）。
            var store5 = ActivationStore.Load(TestKey, seatCount: 5);
            Assert.Equal(5, store5.SeatCount);
            // 既存端末は維持される。
            Assert.Equal(1, store5.UsedSeats);
            Assert.True(store5.ContainsDevice("dev-1"));
        }

        [Fact]
        public void Load_WithoutSeatCount_KeepsRecordedValue()
        {
            // 5 席で作成・保存。
            var store5 = ActivationStore.Load(TestKey, seatCount: 5);
            store5.TryAddDevice("dev-1", "PC-A", "alice");
            store5.Save();

            // seatCount 引数なしの Load はファイル記録値（5）をそのまま読む（席返却用途）。
            var reloaded = ActivationStore.Load(TestKey);
            Assert.Equal(5, reloaded.SeatCount);
            Assert.Equal(1, reloaded.UsedSeats);
        }

        [Fact]
        public void Load_WithSeatCount_Downgrade_UpdatesValueOnly_KeepsExcessDevices()
        {
            // 5 席で 5 端末登録・保存。
            var store5 = ActivationStore.Load(TestKey, seatCount: 5);
            for (int i = 1; i <= 5; i++)
                store5.TryAddDevice($"dev-{i}", $"PC-{i}", $"user-{i}");
            store5.Save();

            // 3 席で Load（ダウングレード）。第1次は値の更新のみ・超過端末の強制解除はしない。
            var store3 = ActivationStore.Load(TestKey, seatCount: 3);
            Assert.Equal(3, store3.SeatCount);
            Assert.Equal(5, store3.UsedSeats); // 超過端末はそのまま残る（縮約は範囲外）。
        }
    }

    // ---------------------------------------------------------------
    // 2-A. TeamSeatService — 席消費オーケストレーション（第2次）
    //   M5 受け入れ条件「3端末OK / 4端末NG」をサービス層で再現する。
    // ---------------------------------------------------------------
    [Collection("ActivationStoreSerial")]
    public class TeamSeatServiceTest : IDisposable
    {
        private readonly string _tempDir;

        public TeamSeatServiceTest()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PDFPuzzleTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            ActivationStore.OverrideBaseDir = _tempDir;
        }

        public void Dispose()
        {
            ActivationStore.OverrideBaseDir = null;
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        private const string TeamKey = "TEAM-LICENSE-KEY-0002";

        // --- 非 Team tier: 席を消費せず常に true、ファイルも作らない ---

        [Theory]
        [InlineData("Personal")]
        [InlineData("Business")]
        public void TryConsumeSeat_NonTeamTier_ReturnsTrue_NoStoreFile(string tierName)
        {
            var tier = Enum.Parse<LicenseTier>(tierName);
            bool ok = TeamSeatService.TryConsumeSeat(
                "PERSONAL-OR-BIZ-KEY", tier, seatCount: 1,
                "dev-x", "PC-X", "user-x");

            Assert.True(ok);
            // 非 Team は ActivationStore に一切触れないこと（ファイル未生成で担保）。
            Assert.Empty(Directory.GetFiles(_tempDir, "*.json"));
        }

        // --- M5 核心: Team・空 store に3端末OK / 4端末目NG ---

        [Fact]
        public void TryConsumeSeat_Team_ThreeDevicesOk_FourthRejected()
        {
            Assert.True(TeamSeatService.TryConsumeSeat(
                TeamKey, LicenseTier.Team, seatCount: 3, "dev-1", "PC-A", "alice"));
            Assert.True(TeamSeatService.TryConsumeSeat(
                TeamKey, LicenseTier.Team, seatCount: 3, "dev-2", "PC-B", "bob"));
            Assert.True(TeamSeatService.TryConsumeSeat(
                TeamKey, LicenseTier.Team, seatCount: 3, "dev-3", "PC-C", "carol"));

            // 4端末目は満席で拒否。
            Assert.False(TeamSeatService.TryConsumeSeat(
                TeamKey, LicenseTier.Team, seatCount: 3, "dev-4", "PC-D", "dave"));

            // store にも 3 席しか乗っていないこと。
            var store = ActivationStore.Load(TeamKey, seatCount: 3);
            Assert.Equal(3, store.UsedSeats);
            Assert.False(store.ContainsDevice("dev-4"));
        }

        // --- v0.2: 席数パラメタライズド（N 端末 OK / N+1 端末 NG） ---

        [Theory]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(10)]
        public void TryConsumeSeat_Team_NDevicesOk_NPlus1Rejected(int seatCount)
        {
            // N 端末まで席消費に成功する。
            for (int i = 1; i <= seatCount; i++)
            {
                Assert.True(TeamSeatService.TryConsumeSeat(
                    TeamKey, LicenseTier.Team, seatCount,
                    $"dev-{i}", $"PC-{i}", $"user-{i}"));
            }

            // N+1 端末目は満席で拒否。
            Assert.False(TeamSeatService.TryConsumeSeat(
                TeamKey, LicenseTier.Team, seatCount,
                $"dev-{seatCount + 1}", $"PC-{seatCount + 1}", $"user-{seatCount + 1}"));

            var store = ActivationStore.Load(TeamKey, seatCount);
            Assert.Equal(seatCount, store.UsedSeats);
            Assert.False(store.ContainsDevice($"dev-{seatCount + 1}"));
        }

        // --- v0.2: アップグレード反映（3席 store を 5席で Load し直すと 4・5 端末目が入る） ---

        [Fact]
        public void TryConsumeSeat_Upgrade_3To5_AllowsAdditionalSeats()
        {
            // 3 席で 3 端末まで埋める。
            for (int i = 1; i <= 3; i++)
            {
                Assert.True(TeamSeatService.TryConsumeSeat(
                    TeamKey, LicenseTier.Team, seatCount: 3,
                    $"dev-{i}", $"PC-{i}", $"user-{i}"));
            }
            // 3 席のままなら 4 端末目は拒否。
            Assert.False(TeamSeatService.TryConsumeSeat(
                TeamKey, LicenseTier.Team, seatCount: 3, "dev-4", "PC-4", "user-4"));

            // 5 席へアップグレード（verify 由来の席数が 5 に変わった想定）。
            // 4・5 端末目が登録できる。
            Assert.True(TeamSeatService.TryConsumeSeat(
                TeamKey, LicenseTier.Team, seatCount: 5, "dev-4", "PC-4", "user-4"));
            Assert.True(TeamSeatService.TryConsumeSeat(
                TeamKey, LicenseTier.Team, seatCount: 5, "dev-5", "PC-5", "user-5"));
            // 6 端末目は新席数（5）で拒否。
            Assert.False(TeamSeatService.TryConsumeSeat(
                TeamKey, LicenseTier.Team, seatCount: 5, "dev-6", "PC-6", "user-6"));

            var store = ActivationStore.Load(TeamKey, seatCount: 5);
            Assert.Equal(5, store.SeatCount);
            Assert.Equal(5, store.UsedSeats);
        }

        // --- 既存端末の再 TryConsumeSeat: 席数不変で true ---

        [Fact]
        public void TryConsumeSeat_ExistingDevice_ReturnsTrue_SeatsUnchanged()
        {
            TeamSeatService.TryConsumeSeat(TeamKey, LicenseTier.Team, 3, "dev-1", "PC-A", "alice");
            TeamSeatService.TryConsumeSeat(TeamKey, LicenseTier.Team, 3, "dev-2", "PC-B", "bob");
            TeamSeatService.TryConsumeSeat(TeamKey, LicenseTier.Team, 3, "dev-3", "PC-C", "carol");

            // 満席だが既存端末 dev-2 の再認証は許可される。
            Assert.True(TeamSeatService.TryConsumeSeat(
                TeamKey, LicenseTier.Team, 3, "dev-2", "PC-B", "bob"));

            var store = ActivationStore.Load(TeamKey, seatCount: 3);
            Assert.Equal(3, store.UsedSeats);
        }

        // --- ReleaseSeat 後に空き席が戻り再消費可能 ---

        [Fact]
        public void ReleaseSeat_Team_FreesSeat_AllowsReConsume()
        {
            TeamSeatService.TryConsumeSeat(TeamKey, LicenseTier.Team, 3, "dev-1", "PC-A", "alice");
            TeamSeatService.TryConsumeSeat(TeamKey, LicenseTier.Team, 3, "dev-2", "PC-B", "bob");
            TeamSeatService.TryConsumeSeat(TeamKey, LicenseTier.Team, 3, "dev-3", "PC-C", "carol");
            Assert.False(TeamSeatService.TryConsumeSeat(
                TeamKey, LicenseTier.Team, 3, "dev-4", "PC-D", "dave"));

            // dev-2 の席を返却。
            TeamSeatService.ReleaseSeat(TeamKey, LicenseTier.Team, "dev-2");

            var afterRelease = ActivationStore.Load(TeamKey, seatCount: 3);
            Assert.Equal(2, afterRelease.UsedSeats);
            Assert.False(afterRelease.ContainsDevice("dev-2"));

            // 空き席に dev-4 を再消費できる。
            Assert.True(TeamSeatService.TryConsumeSeat(
                TeamKey, LicenseTier.Team, 3, "dev-4", "PC-D", "dave"));
            Assert.Equal(3, ActivationStore.Load(TeamKey, seatCount: 3).UsedSeats);
        }

        [Fact]
        public void ReleaseSeat_NonTeamTier_IsNoOp()
        {
            // 非 Team は store に触れない（ファイル未生成で担保。例外も投げない）。
            TeamSeatService.ReleaseSeat("PERSONAL-KEY", LicenseTier.Personal, "dev-x");
            TeamSeatService.ReleaseSeat("BIZ-KEY", LicenseTier.Business, "dev-y");
            Assert.Empty(Directory.GetFiles(_tempDir, "*.json"));
        }

        // --- TryConsumeSeat 成功後にファイル保存・ラウンドトリップ一致 ---

        [Fact]
        public void TryConsumeSeat_Team_PersistsStore_RoundTrips()
        {
            Assert.True(TeamSeatService.TryConsumeSeat(
                TeamKey, LicenseTier.Team, 3, "dev-1", "PC-A", "alice"));

            // Save 済みでファイルが存在すること。
            var files = Directory.GetFiles(_tempDir, "*.json");
            Assert.Single(files);

            // 別 Load でラウンドトリップ一致。
            var reloaded = ActivationStore.Load(TeamKey, seatCount: 3);
            Assert.Equal(1, reloaded.UsedSeats);
            Assert.Equal(LicenseTier.Team, reloaded.Tier);
            Assert.True(reloaded.ContainsDevice("dev-1"));
            var dev1 = reloaded.Devices.First(d => d.DeviceId == "dev-1");
            Assert.Equal("PC-A", dev1.MachineName);
            Assert.Equal("alice", dev1.UserName);
        }
    }

    // ---------------------------------------------------------------
    // 3-A. LogService.StartRun — 監査フィールドの自動付与（第3次）
    // ---------------------------------------------------------------
    public class LogServiceAuditFieldsTest
    {
        [Fact]
        public void StartRun_PopulatesUserName_MatchesEnvironment()
        {
            var run = LogService.StartRun(null);
            Assert.Equal(Environment.UserName, run.UserName);
        }

        [Fact]
        public void StartRun_PopulatesDeviceId_MatchesDeviceIdentifier()
        {
            var run = LogService.StartRun(null);
            Assert.Equal(DeviceIdentifier.GetCurrent(), run.DeviceId);
            Assert.NotNull(run.DeviceId);
            Assert.Equal(16, run.DeviceId!.Length);
            Assert.Matches("^[0-9a-f]{16}$", run.DeviceId);
        }

        [Fact]
        public void StartRun_PopulatesLicenseTierName_IsKnownTier()
        {
            var run = LogService.StartRun(null);
            Assert.NotNull(run.LicenseTierName);
            Assert.Contains(run.LicenseTierName, new[] { "Personal", "Business", "Team" });
        }

        [Fact]
        public void StartRun_PreservesOutputFolder()
        {
            var run = LogService.StartRun(@"C:\out");
            Assert.Equal(@"C:\out", run.OutputFolder);
        }
    }

    // ---------------------------------------------------------------
    // 3-B. RunLogEntry — 旧スキーマ JSON の後方互換（第3次）
    // ---------------------------------------------------------------
    public class RunLogEntryBackCompatTest
    {
        [Fact]
        public void Deserialize_LegacyJsonWithoutAuditFields_AuditFieldsAreNull()
        {
            // 監査フィールド3つを持たない旧スキーマ相当の JSON。
            var json = """{"RunId":"abc-123","StartedAt":"2026-01-01T10:00:00","Steps":[]}""";
            var run = JsonSerializer.Deserialize<RunLogEntry>(json);

            Assert.NotNull(run);
            Assert.Equal("abc-123", run!.RunId);
            Assert.Null(run.UserName);
            Assert.Null(run.DeviceId);
            Assert.Null(run.LicenseTierName);
        }
    }

    // ---------------------------------------------------------------
    // 3-C. LogService.BuildAuditCsv — 監査 CSV 行組み立て（第3次）
    // ---------------------------------------------------------------
    public class BuildAuditCsvTest
    {
        private static StepLogEntry MakeStep(string key) => new()
        {
            MethodKey = key,
            MethodName = key,
            StartedAt = new DateTime(2026, 1, 1, 10, 0, 0),
            CompletedAt = new DateTime(2026, 1, 1, 10, 0, 5),
            Success = true,
        };

        [Fact]
        public void BuildAuditCsv_Header_Has18Columns_LastThreeAreAuditColumns()
        {
            var (csv, _) = LogService.BuildAuditCsv(new List<RunLogEntry>());
            var header = csv.Split('\n')[0].TrimEnd('\r');
            var cols = header.Split(',');

            Assert.Equal(18, cols.Length);
            Assert.Equal("UserName", cols[15]);
            Assert.Equal("DeviceId", cols[16]);
            Assert.Equal("LicenseTierName", cols[17]);
        }

        [Fact]
        public void BuildAuditCsv_RunWithSteps_DataRowsCarryAuditValues()
        {
            var run = new RunLogEntry
            {
                RunId = "run-1",
                UserName = "alice",
                DeviceId = "abcdef0123456789",
                LicenseTierName = "Team",
                Steps = { MakeStep("merge"), MakeStep("split") },
            };

            var (csv, rowCount) = LogService.BuildAuditCsv(new[] { run });
            Assert.Equal(2, rowCount);

            var lines = csv.Split('\n').Where(l => l.Length > 0).Select(l => l.TrimEnd('\r')).ToArray();
            // [0] = header, [1]/[2] = data rows
            foreach (var dataLine in new[] { lines[1], lines[2] })
            {
                var cols = dataLine.Split(',');
                Assert.Equal(18, cols.Length);
                Assert.Equal("alice", cols[15]);
                Assert.Equal("abcdef0123456789", cols[16]);
                Assert.Equal("Team", cols[17]);
            }
        }

        [Fact]
        public void BuildAuditCsv_RunWithZeroSteps_EmitsOneRow_WithAuditValues()
        {
            var run = new RunLogEntry
            {
                RunId = "run-empty",
                UserName = "bob",
                DeviceId = "0011223344556677",
                LicenseTierName = "Business",
            };

            var (csv, rowCount) = LogService.BuildAuditCsv(new[] { run });
            Assert.Equal(1, rowCount);

            var lines = csv.Split('\n').Where(l => l.Length > 0).Select(l => l.TrimEnd('\r')).ToArray();
            var cols = lines[1].Split(',');
            Assert.Equal(18, cols.Length);
            Assert.Equal("bob", cols[15]);
            Assert.Equal("0011223344556677", cols[16]);
            Assert.Equal("Business", cols[17]);
        }

        [Fact]
        public void BuildAuditCsv_NullUserName_EmitsEmptyString()
        {
            var run = new RunLogEntry
            {
                RunId = "run-null",
                UserName = null,
                DeviceId = null,
                LicenseTierName = null,
            };

            var (csv, _) = LogService.BuildAuditCsv(new[] { run });
            var lines = csv.Split('\n').Where(l => l.Length > 0).Select(l => l.TrimEnd('\r')).ToArray();
            var cols = lines[1].Split(',');

            Assert.Equal(18, cols.Length);
            Assert.Equal("", cols[15]);
            Assert.Equal("", cols[16]);
            Assert.Equal("", cols[17]);
        }

        [Fact]
        public void BuildAuditCsv_UserNameWithCommaAndQuote_IsEscaped()
        {
            var run = new RunLogEntry
            {
                RunId = "run-esc",
                UserName = "doe, \"jane\"",
                DeviceId = "ffffffffffffffff",
                LicenseTierName = "Team",
            };

            var (csv, _) = LogService.BuildAuditCsv(new[] { run });
            var lines = csv.Split('\n').Where(l => l.Length > 0).Select(l => l.TrimEnd('\r')).ToArray();
            var dataLine = lines[1];

            // カンマ・ダブルクォートを含む UserName が EscapeCsv 経由でクォートされること。
            // クォート内のカンマで列が割れないよう、クォート済みフィールドを含む形を検証。
            Assert.Contains("\"doe, \"\"jane\"\"\"", dataLine);
        }
    }
}
