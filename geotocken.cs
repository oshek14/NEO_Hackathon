using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.ComponentModel;
using System.Numerics;

namespace Neo.SmartContract
{
    public class GEOTOKEN : Framework.SmartContract
    {

        public static string Name() => "Georgia Token";
        public static string Symbol() => "GEO";
        public static readonly byte[] Owner = "AK2nJJpJr6o664CWJKi1QRXjqeic2zRp8y".ToScriptHash();
        private static readonly byte[] neo_asset_id = { 155, 124, 255, 218, 166, 116, 190, 174, 15, 147, 14, 190, 96, 133, 175, 144, 147, 229, 254, 86, 179, 74, 92, 34, 12, 205, 207, 110, 252, 51, 111, 197 };
        private const ulong neo_decimals = 100000000;
        public static byte Decimals() => 8;
        private const ulong factor = 100000000; //decided by Decimals()
        private const ulong total_amount = 100000000 * factor; //token amount
        private const ulong swap_rate = 10000 * factor;

        [DisplayName("transfer")]
        public static event Action<byte[], byte[], BigInteger> Transferred;

        [DisplayName("refund")]
        public static event Action<byte[], BigInteger> Refund;

        public static Object Main(string operation, params object[] args)
        {
            if (Runtime.Trigger == TriggerType.Verification)
            {
                if (Owner.Length == 20)
                {
                    // if param Owner is script hash
                    return Runtime.CheckWitness(Owner);
                }
                else if (Owner.Length == 33)
                {
                    // if param Owner is public key
                    byte[] signature = operation.AsByteArray();
                    return VerifySignature(signature, Owner);
                }
            }
            else if (Runtime.Trigger == TriggerType.Application)
            {
                if (operation == "deploy") return Deploy();
                if (operation == "totalSupply") return TotalSupply();
                if (operation == "name") return Name();
                if (operation == "symbol") return Symbol();
                if (operation == "decimals") return Decimals();
                if (operation == "transfer")
                {
                    if (args.Length != 3 || args[0] == null || ((byte[])args[0]).Length == 0 || args[1] == null || ((byte[])args[1]).Length == 0) return NotifyErrorAndReturnFalse("argument count must be 3 and they must not be null");
                    byte[] from = (byte[])args[0];
                    byte[] to = (byte[])args[1];
                    BigInteger value = (BigInteger)args[2];
                    return Transfer(from, to, value);
                }
                if (operation == "balanceOf")
                {
                    if (args.Length != 1 || args[0] == null || ((byte[])args[0]).Length == 0) return NotifyErrorAndReturn0("argument count must be 1 and they must not be null");
                    byte[] account = (byte[])args[0];
                    return BalanceOf(account);
                }
                if (operation == "add_location")
                {
                    if (args.Length != 2) return NotifyErrorAndReturnFalse("You must pass 2 arguments");
                    return AddNewLocation((string)args[0], (ulong)args[1]);
                }
                if (operation == "make_check_in")
                {
                    if (args.Length != 2) return NotifyErrorAndReturnFalse("You must pass 2 arguments");
                    return MakeCheckIn((string)args[0], (byte[])args[1]);
                }
                if (operation == "all_places") return GetPlaces();
                if (operation == "get_place_token") return GetPlaceToken((byte[])args[0]);
                if (operation == "get_place_checkin") return GetPlaceCheckin((byte[])args[0]);
                if (operation == "buy_allowance") return BuyAllowence();
                if (operation == "transfer_from")
                {
                    if (args.Length != 3 || args[0] == null || ((byte[])args[0]).Length == 0 || args[1] == null || ((byte[])args[1]).Length == 0) return NotifyErrorAndReturnFalse("argument count must be 3 and they must not be null");
                    return TransferFrom((byte[])args[0], (byte[])args[1], (BigInteger)args[0]);
                }
            }

            return NotifyErrorAndReturnFalse("Operation not found");

        }





        public static bool BuyAllowence()
        {
            byte[] sender = GetSender();
            if (sender == Owner) return true;
            if (sender.Length == 0 || sender.Length != 20) return false;
            ulong contribute_value = GetContributedValue();
            ulong token_to_allow = contribute_value / neo_decimals * swap_rate;
            BigInteger tokensOut = Storage.Get(Storage.CurrentContext, "tockensOut").AsBigInteger();
            BigInteger totalSupply = Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();

            if (Storage.Get(Storage.CurrentContext, Owner).AsBigInteger() - tokensOut < token_to_allow)
            {
                Refund(sender, contribute_value);
                return false;
            }
            Storage.Put(Storage.CurrentContext, Owner.Concat(sender), token_to_allow);
            Storage.Put(Storage.CurrentContext, "tockensOut", tokensOut + token_to_allow);
            return true;
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
                if (output.ScriptHash == ExecutionEngine.ExecutingScriptHash && output.AssetId == neo_asset_id)
                {
                    value += (ulong)output.Value;
                }
            }
            return value;
        }

        public static bool Deploy()
        {
            int forHash = 1434312;
            if (!Runtime.CheckWitness(Owner)) return NotifyErrorAndReturnFalse("You are not the Owner of this Smart Contract");
            byte[] total_supply = Storage.Get(Storage.CurrentContext, "totalSupply");
            if (total_supply != null) return NotifyErrorAndReturnFalse("Looks like this method has been allready used");
            Storage.Put(Storage.CurrentContext, Owner, total_amount);
            Storage.Put(Storage.CurrentContext, "totalSupply", total_amount);
            Storage.Put(Storage.CurrentContext, "tockensOut", 0);

            Transferred(null, Owner, total_amount);
            return true;
        }

        public static BigInteger TotalSupply()
        {
            return Storage.Get(Storage.CurrentContext, "totalSupply").AsBigInteger();
        }

        public static bool Transfer(byte[] from, byte[] to, BigInteger value)
        {
            if (to == null || to.Length != 20) return NotifyErrorAndReturnFalse("To value must not be empty and have size of 20");
            if (from == null || from.Length != 20) return NotifyErrorAndReturnFalse("From value must not be empty and have size of 20");
            if (value <= 0) return NotifyErrorAndReturnFalse("Try to send more than 0 tokens");
            if (!Runtime.CheckWitness(from)) return NotifyErrorAndReturnFalse("Owner of the wallet is not involved in this invoke");
            if (from == to) return true;
            BigInteger from_value = Storage.Get(Storage.CurrentContext, from).AsBigInteger();
            if (from_value < value) return NotifyErrorAndReturnFalse("Insufficient funds");
            if (from == Owner)
            {
                BigInteger tokensOut = Storage.Get(Storage.CurrentContext, "tockensOut").AsBigInteger();
                if (from_value < value + tokensOut) return NotifyErrorAndReturnFalse("Insufficient funds");
            }
            if (from_value == value) Storage.Delete(Storage.CurrentContext, from);
            else Storage.Put(Storage.CurrentContext, from, from_value - value);
            BigInteger to_value = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
            Storage.Put(Storage.CurrentContext, to, to_value + value);
            Transferred(from, to, value);
            return true;
        }

        public static bool TransferFrom(byte[] originator, byte[] to, BigInteger amountToSend)
        {
            if (!(Runtime.CheckWitness(originator))) return NotifyErrorAndReturnFalse("You are not allowed");
            if (amountToSend <= 0) return NotifyErrorAndReturnFalse("not funds specified");
            if (to.Length == 0 || to.Length != 20) return NotifyErrorAndReturnFalse("bad address");
            BigInteger allowedAmount = Storage.Get(Storage.CurrentContext, Owner.Concat(originator)).AsBigInteger();
            if (amountToSend > allowedAmount) return NotifyErrorAndReturnFalse("You are not allowed to spend that much tokens");
            if (allowedAmount == 0) return NotifyErrorAndReturnFalse("You are not allowed");

            BigInteger to_balance = Storage.Get(Storage.CurrentContext, to).AsBigInteger();
            Storage.Put(Storage.CurrentContext, to, to_balance + amountToSend);

            BigInteger tokensOut = Storage.Get(Storage.CurrentContext, "tockensOut").AsBigInteger();
            Storage.Put(Storage.CurrentContext, "tockensOut", tokensOut - amountToSend);
            if (amountToSend == allowedAmount) Storage.Delete(Storage.CurrentContext, Owner.Concat(originator));
            else Storage.Put(Storage.CurrentContext, Owner.Concat(originator), allowedAmount - amountToSend);

            return true;
        }

        public static BigInteger BalanceOf(byte[] address)
        {
            return Storage.Get(Storage.CurrentContext, address).AsBigInteger();
        }

        public static bool AddNewLocation(string name, ulong howManyToken)
        {
            if (!Runtime.CheckWitness(Owner)) return false;

            byte[] ifTokenExists = Storage.Get(Storage.CurrentContext, name + "_token");

            if (ifTokenExists != null) return NotifyErrorAndReturnFalse("this place already has the token attached");

            byte[] allPlaces = Storage.Get(Storage.CurrentContext, "places");

            if (allPlaces == null) Storage.Put(Storage.CurrentContext, "places", name);

            else
            {
                byte[] prev_sum = "**".AsByteArray().Concat(name.AsByteArray());
                byte[] sum = allPlaces.Concat(prev_sum);
                Storage.Put(Storage.CurrentContext, "places", sum.AsString());
            }

            Storage.Put(Storage.CurrentContext, name + "_token", howManyToken);
            return true;
        }





        public static bool MakeCheckIn(string name, byte[] to)
        {
            if (to == Owner) return NotifyErrorAndReturnFalse("You're already the owner. Can't invoke");
            BigInteger getTokenCount = Storage.Get(Storage.CurrentContext, name + "_token").AsBigInteger();
            if (getTokenCount == null) return NotifyErrorAndReturnFalse("There was an error.");

            BigInteger total = TotalSupply();
            if (total < getTokenCount) return NotifyErrorAndReturnFalse("Government has exceeded the total token supply");

            BigInteger getCheckinCount = Storage.Get(Storage.CurrentContext, name + "_count").AsBigInteger();
            if (getCheckinCount == null) Storage.Put(Storage.CurrentContext, name + "_count", 0);
            else Storage.Put(Storage.CurrentContext, name + "_count", getCheckinCount + 1);

            BigInteger ownerTokenCount = Storage.Get(Storage.CurrentContext, Owner).AsBigInteger();
            Storage.Put(Storage.CurrentContext, Owner, (ownerTokenCount - getTokenCount).AsByteArray());

            byte[] toTokenCount = Storage.Get(Storage.CurrentContext, to);
            if (toTokenCount == null)
            {
                Storage.Put(Storage.CurrentContext, to, getTokenCount);
            }
            else
            {
                BigInteger bigint = toTokenCount.AsBigInteger() + getTokenCount;
                Storage.Put(Storage.CurrentContext, to, bigint);
            }

            Transferred(Owner, to, getTokenCount);
            return true;

        }




        public static byte[] GetPlaces()
        {
            return Storage.Get(Storage.CurrentContext, "places");
        }

        public static byte[] GetPlaceToken(byte[] place)
        {
            byte[] getTokenCount = Storage.Get(Storage.CurrentContext, place + "_token");
            return getTokenCount;
        }

        public static byte[] GetPlaceCheckin(byte[] place)
        {
            byte[] getCheckinCount = Storage.Get(Storage.CurrentContext, place + "_count");
            return getCheckinCount;
        }

        public static bool NotifyErrorAndReturnFalse(string value)
        {
            Runtime.Notify(value);
            return false;
        }

        public static int NotifyErrorAndReturn0(string value)
        {
            Runtime.Notify(value);
            return 0;
        }

        public static bool addVideoCommercial(byte[] address, string hash)
        {


            return true;
        }

    }
}