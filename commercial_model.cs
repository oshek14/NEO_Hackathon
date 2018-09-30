using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.Numerics;
using System.ComponentModel;

namespace NeoContract14
{
    public class Contract1 : SmartContract
    {
        private static readonly byte[] neo_asset_id = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };
        private static readonly byte[] geotocken_asset_id = { 112, 88, 255, 218, 121, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 198 };
        private const int num_commercials = 5;

        [DisplayName("refund")]
        public static event Action<byte[], BigInteger> Refund;

        public static Object Main(string operation, params object[] args)
        {

            if (operation == "deploy") return Deploy();
            if (operation == "buy_allowence") return BuyAllowence();
            if (operation == "get_video") return GetVideo();
            if (operation == "buy_commercial")
            {
                if (args.Length != 1 || args[0] == null || ((byte[])args[0]).Length == 0) return NotifyErrorAndReturnFalse("argument count must be 1 and they must not be null");
                string video = (string)args[0];
                return BuyCommercial(video);
            }

            byte[] sender = GetSender();
            ulong contribute_value = GetContributedValue();
            if (contribute_value > 0 && sender.Length != 0)
            {
                Refund(sender, contribute_value);
            }
            return NotifyErrorAndReturnFalse("Operation not found");
        }

        public static bool Deploy()
        {
            BigInteger index = 1;
            Storage.Put(Storage.CurrentContext, "currentPosition", index.AsByteArray());
            return true;
        }

        public static bool BuyAllowence()
        {
            byte[] sender = GetSender();

            if (sender.Length == 0 || sender.Length != 20) return false;
            ulong contribute_value = GetContributedValue();
            if (contribute_value == 10)
            {
                Storage.Put(Storage.CurrentContext, sender, "active".AsByteArray());
                Runtime.Notify("permission granted");
                return true;
            }
            else
            {
                Refund(sender, contribute_value);
                return false;
            }
        }

        public static bool BuyCommercial(string video)
        {
            byte[] sender = GetSender();
            BigInteger currentTime = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp;

            BigInteger contribute_value = GetContributedValue();
            BigInteger endTime = currentTime + contribute_value * 1000 * 60 * 30;
            bool isPlace = false;
            for (int i = 1; i <= num_commercials; i++)
            {
                string num = i.ToString();
                string key = ("commercial_status_".AsByteArray().Concat(num.AsByteArray())).AsString();
                byte[] status = Storage.Get(Storage.CurrentContext, key);
                if (status == null)
                {
                    string commercial_key = ("commercial_".AsByteArray().Concat(num.AsByteArray())).AsString();
                    Storage.Put(Storage.CurrentContext, key, endTime.AsByteArray());
                    Storage.Put(Storage.CurrentContext, commercial_key, video.AsByteArray());
                    isPlace = true;
                    break;
                }
            }
            if (!isPlace)
            {
                Refund(sender, contribute_value);
                return false;
            }
            else return true;
        }

        public static string GetVideo()
        {
            FilterCommercials();
            string result = "";
            BigInteger index = Storage.Get(Storage.CurrentContext, "currentPosition").AsBigInteger();
            for (int i = 0; i < num_commercials; i++)
            {
                string num = ((int)index).ToString();
                string commercial_key = ("commercial_".AsByteArray().Concat(num.AsByteArray())).AsString();
                byte[] video = Storage.Get(Storage.CurrentContext, commercial_key);
                if (video == null)
                {
                    if (index == num_commercials) index = 1;
                    else index = index + 1;
                }
                else
                {
                    result = video.AsString();
                    break;
                }
            }
            return result;
        }

        private static void FilterCommercials()
        {
            BigInteger currentTime = Blockchain.GetHeader(Blockchain.GetHeight()).Timestamp;

            for (int i = 1; i <= num_commercials; i++)
            {
                string num = i.ToString();
                string key = ("commercial_status_".AsByteArray().Concat(num.AsByteArray())).AsString();
                BigInteger end_time = Storage.Get(Storage.CurrentContext, key).AsBigInteger();
                if (end_time != null)
                {
                    if (currentTime >= end_time)
                    {
                        string commercial_key = ("commercial_".AsByteArray().Concat(num.AsByteArray())).AsString();
                        Storage.Delete(Storage.CurrentContext, key);
                        Storage.Delete(Storage.CurrentContext, commercial_key);
                    }
                }
            }
        }

        private static byte[] GetSender()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] references = tx.GetReferences();
            foreach (TransactionOutput output in references)
            {
                if (output.AssetId == neo_asset_id) return output.ScriptHash;
            }
            return new byte[] { };
        }

        private static ulong GetContributedValue()
        {
            Transaction tx = (Transaction)ExecutionEngine.ScriptContainer;
            TransactionOutput[] outputs = tx.GetOutputs();
            ulong value = 0;
            foreach (TransactionOutput output in outputs)
            {
                if (output.ScriptHash == ExecutionEngine.ExecutingScriptHash && output.AssetId == geotocken_asset_id)
                {
                    value += (ulong)output.Value;
                }
            }
            return value;
        }
        private static bool NotifyErrorAndReturnFalse(string value)
        {
            Runtime.Notify(value);
            return false;
        }
    }
}
