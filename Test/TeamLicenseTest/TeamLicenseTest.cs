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

        // --- ResolveSeatCount() ---

        [Fact]
        public void ResolveSeatCount_QuantityField_ReturnsQuantity()
        {
            var root = Parse("""{ "purchase": { "quantity": 5 } }""");
            Assert.Equal(5, LicenseTierResolver.ResolveSeatCount(root, LicenseTier.Team));
        }

        [Fact]
        public void ResolveSeatCount_VariantsAndQuantityFallback_ReturnsParsedNumber()
        {
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
            var tier = Enum.Parse<LicenseTier>(tierName);
            var root = Parse("""{ "purchase": { "quantity": 5 } }""");
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
    // 1-D. ActivationStore — M5 受け入れ条件の核心
    // ---------------------------------------------------------------
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
    }
}
