﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using DoormatCore.Games;
using DoormatCore.Helpers;

namespace DoormatCore.Sites
{
    public class BetKing : BaseSite
    {
        string accesstoken = "";
        DateTime LastSeedReset = new DateTime();
        public bool ispd = false;
        string username = "";
        long uid = 0;
        DateTime lastupdate = new DateTime();
        HttpClient Client;// = new HttpClient { BaseAddress = new Uri("https://api.primedice.com/api/") };
        HttpClientHandler ClientHandlr;
        CookieContainer cookies = new CookieContainer();
        string clientseed = "";
        bkGetCurrencies Currs = null;
        BKCurrency CurCurrency = null;
        public static string[] sCurrencies = new string[] { "Btc", "Eth", "BKB", "Ltc"/*,"OMG",
"TRX",
"EOS",
"SNT",
"PPT",
"GNT",
"REP",
"VERI",
"SALT",
"BAT",
"FUN",
"POWR",
"PAY",
"ZRX",
"CVC" */};
        int nonce = 0;
        string serverseedhash = "";

        Dictionary<string, int> Curs = new Dictionary<string, int>();

        public BetKing()
        {
            Curs.Add("Btc", 0);
            Curs.Add("Eth", 1);
            Curs.Add("Ltc", 3);
            Curs.Add("BKB", 6);
            /*Curs.Add("OmiseGo", 7);
            Curs.Add("TRON", 8);
            Curs.Add("EOS", 9);
            Curs.Add("Status", 11);
            Curs.Add("Populous", 12);
            Curs.Add("Golem", 13);
            Curs.Add("Augur", 15);
            Curs.Add("Veritaseum", 16);
            Curs.Add("SALT", 17);
            Curs.Add("Basic Attention Token", 18);
            Curs.Add("FunFair", 19);
            Curs.Add("Power Ledger", 21);
            Curs.Add("TenX", 24);
            Curs.Add("0x", 25);
            Curs.Add("CIVIC", 28);*/

            StaticLoginParams = new LoginParameter[] { new LoginParameter("Username/Email",false,true,false,true), new LoginParameter("Password", true, true, false, true), new LoginParameter("2FA Code", false, false, true, true, true) };
            this.MaxRoll = 99.99m;
            this.SiteAbbreviation = "DD";
            this.SiteName = "DuckDice";
            this.SiteURL = "https://betking.io?ref=u:seuntjie";
            this.Stats = new SiteStats();
            this.TipUsingName = true;
            this.AutoInvest = false;
            this.AutoWithdraw = false;
            this.CanChangeSeed = true;
            this.CanChat = false;
            this.CanGetSeed = true;
            this.CanRegister = false;
            this.CanSetClientSeed = false;
            this.CanTip = false;
            this.CanVerify = true;
            this.Currencies = sCurrencies;
            SupportedGames = new Games.Games[] { Games.Games.Dice};
            this.Currency = 0;
            this.DiceBetURL = "https://betking.io/bets/{0}";
            this.Edge = 1m;

        }

        public override void SetProxy(ProxyDetails ProxyInfo)
        {
            throw new NotImplementedException();
        }

        protected override void _Disconnect()
        {
            ispd = false;
        }
        void GetBlanaceThread()
        {
            while (ispd)
            {
                if ((DateTime.Now - lastupdate).TotalSeconds > 25 || ForceUpdateStats)
                {
                    ForceUpdateStats = false;
                    lastupdate = DateTime.Now;
                    UpdateStats();
                    //Sock.Send("2");
                }
                Thread.Sleep(1000);
            }
        }
        void GetBalance()
        {
            if (clientseed == null)
                clientseed = R.Next(0, int.MaxValue).ToString();
            HttpResponseMessage Msg = Client.GetAsync("api/wallet/balances").Result;
            if (Msg.IsSuccessStatusCode)
            {
                string Response = Msg.Content.ReadAsStringAsync().Result;
                bkGetBalances tmp = json.JsonDeserialize<bkGetBalances>(Response);
                foreach (var x in tmp.balances)
                {

                    if (CurCurrency.id == x.currency && CurCurrency.symbol.ToLower() == CurrentCurrency.ToLower())
                    {
                        Stats.Balance = decimal.Parse(x.balance, System.Globalization.NumberFormatInfo.InvariantInfo) / (decimal)CurCurrency.EffectiveScale;

                    }
                }
            }
        }



        void GetStats()
        {
            HttpResponseMessage Msg = Client.GetAsync("https://betking.io/socket-api/stats/my-stats").Result;
            if (Msg.IsSuccessStatusCode)
            {
                string Response = Msg.Content.ReadAsStringAsync().Result;

                BKStat[] tmp = json.JsonDeserialize<BKStat[]>(Response);
                foreach (BKStat x in tmp)
                {
                    foreach (BKCurrency y in Currs.currencies)
                    {
                        if (CurCurrency.id == x.currency && CurCurrency.symbol.ToLower() == CurrentCurrency.ToLower())
                        {
                            Stats.Profit = decimal.Parse(x.profit, System.Globalization.NumberFormatInfo.InvariantInfo) / (decimal)CurCurrency.EffectiveScale;
                            Stats.Bets = int.Parse(x.num_bets, System.Globalization.NumberFormatInfo.InvariantInfo);
                            Stats.Wagered = decimal.Parse(x.wagered, System.Globalization.NumberFormatInfo.InvariantInfo) / (decimal)CurCurrency.EffectiveScale;
                        }
                    }

                }
                string LoadState = Client.GetStringAsync("api/dice/load-state?clientSeed=" + R.Next(0, int.MaxValue) + "&currency=0").Result;
                bkLoadSTate TmpState = json.JsonDeserialize<bkLoadSTate>(LoadState);
                nonce = TmpState.nonce;
                clientseed = TmpState.clientSeed;
                serverseedhash = TmpState.serverSeedHash;


            }
        }
        
        protected override void _Login(LoginParamValue[] LoginParams)
        {
            try
            {
                string Username="",Password="",otp=""; 
                foreach (LoginParamValue x in LoginParams)
                {
                    if (x.Param.Name.ToLower() == "username/email")
                        Username = x.Value;
                    if (x.Param.Name.ToLower() == "password")
                        Password = x.Value;
                    if (x.Param.Name.ToLower() == "2fa code")
                        otp = x.Value;
                }
                ClientHandlr = new HttpClientHandler { UseCookies = true, AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip, AllowAutoRedirect = true };
                Client = new HttpClient(ClientHandlr) { BaseAddress = new Uri("https://betking.io/") };
                Client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
                Client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("deflate"));
                cookies = new CookieContainer();
                ClientHandlr.CookieContainer = cookies;
                Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/62.0.3202.94 Safari/537.36");

                string s1 = "";
                HttpResponseMessage resp = Client.GetAsync("").Result;
                if (resp.IsSuccessStatusCode)
                {
                    s1 = resp.Content.ReadAsStringAsync().Result;
                }
                else
                {
                    if (resp.StatusCode == HttpStatusCode.ServiceUnavailable)
                    {
                        s1 = resp.Content.ReadAsStringAsync().Result;
                        /*
                        if (!Cloudflare.doCFThing(s1, Client, ClientHandlr, 0, "betking.io"))
                        {
                            finishedlogin(false);
                            return;
                        }*/
                    }
                    else
                    {

                    }
                }
                resp = Client.GetAsync("").Result;
                s1 = resp.Content.ReadAsStringAsync().Result;

                s1 = s1.Substring(s1.IndexOf("window.settings"));
                s1 = s1.Substring(s1.IndexOf("\"csrfToken\":\"") + "\"csrfToken\":\"".Length);

                string csrf = s1.Substring(0, s1.IndexOf("\""));
                Client.DefaultRequestHeaders.Add("csrf-token", csrf);

                List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();
                // pairs.Add(new KeyValuePair<string, string>("_csrf", csrf));
                //pairs.Add(new KeyValuePair<string, string>("client_id", "0"));
                pairs.Add(new KeyValuePair<string, string>("fingerprint", "DiceBot-" + Process.GetCurrentProcess().Id));
                pairs.Add(new KeyValuePair<string, string>("loginmethod", Username.Contains("@") ? "email" : "username"));
                pairs.Add(new KeyValuePair<string, string>("password", Password));
                //pairs.Add(new KeyValuePair<string, string>("redirect_uri", "https://betking.io/bet"));
                pairs.Add(new KeyValuePair<string, string>("otp", otp));
                pairs.Add(new KeyValuePair<string, string>("rememberme", "false"));
                pairs.Add(new KeyValuePair<string, string>(Username.Contains("@") ? "email" : "username", Username));
                FormUrlEncodedContent Content = new FormUrlEncodedContent(pairs);
                HttpResponseMessage RespMsg = Client.PostAsync("api/auth/login", Content).Result;
                string responseUri = RespMsg.RequestMessage.RequestUri.ToString();
                string sEmitResponse = RespMsg.Content.ReadAsStringAsync().Result;

                if (!sEmitResponse.ToLower().Contains("error"))
                {
                    BKAccount tmpAccount = json.JsonDeserialize<BKAccount>(sEmitResponse);
                    this.username = Username;

                    sEmitResponse = Client.GetStringAsync("api/wallet/currencies").Result;
                    Currs = json.JsonDeserialize<bkGetCurrencies>(sEmitResponse);

                    if (Currs == null)
                    {
                        Logger.DumpLog("Failed to get currencies", 0);
                        callError("Failed to get currencies", true, ErrorType.Unknown);
                        callLoginFinished(false);
                        return;
                    }
                    foreach (BKCurrency x in Currs.currencies)
                    {
                        if (x.symbol.ToLower() == CurrentCurrency.ToLower())
                        {
                            CurCurrency = x;
                        }
                    }
                    resp = Client.GetAsync("bet/dice").Result;
                    s1 = resp.Content.ReadAsStringAsync().Result;

                    s1 = s1.Substring(s1.IndexOf("window.settings"));
                    s1 = s1.Substring(s1.IndexOf("\"csrfToken\":\"") + "\"csrfToken\":\"".Length);

                    csrf = s1.Substring(0, s1.IndexOf("\""));
                    Client.DefaultRequestHeaders.Remove("csrf-token");
                    Client.DefaultRequestHeaders.Add("csrf-token", csrf);
                    GetBalance();
                    GetStats();


                    string LoadState = Client.GetStringAsync("api/dice/load-state?clientSeed=" + R.Next(0, int.MaxValue) + "&currency=0").Result;
                    bkLoadSTate TmpState = json.JsonDeserialize<bkLoadSTate>(LoadState);
                    nonce = TmpState.nonce;
                    clientseed = TmpState.clientSeed;
                    serverseedhash = TmpState.serverSeedHash;
                    callLoginFinished(true);
                    return;

                }

            }
            catch (Exception e)
            {
                Logger.DumpLog(e);
                
                callLoginFinished(false);
                return;
            }
            callLoginFinished(false);
        }

        protected override void _UpdateStats()
        {
            GetBalance();
            GetStats();
        }

        protected override void _PlaceDiceBet(PlaceDiceBet BetDetails)
        {
            try
            {
                
               string loginjson = "{\"betAmount\":\"" + (BetDetails.Amount * (decimal)CurCurrency.EffectiveScale).ToString("0") +
                    "\",\"currency\":" + CurCurrency.id.ToString() +
                    ",\"target\":" + (BetDetails.High ? "1" : "0") + ",\"chance\":" + BetDetails.Chance.ToString("0.#####") + "}";

                HttpContent cont = new StringContent(loginjson);
                cont.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                HttpResponseMessage tmpmsg = Client.PostAsync("api/dice/bet", cont).Result;
                string sEmitResponse = tmpmsg.Content.ReadAsStringAsync().Result;
                bkPlaceBet tmpBet = json.JsonDeserialize<bkPlaceBet>(sEmitResponse);
                if (tmpBet.error != null)
                {
                    Logger.DumpLog(tmpBet.error, 1);
                    if (tmpBet.error == ("MIN_BET_AMOUNT_FOR_CURRENCYNAME_IS"))
                    {

                        callError("Bet too small",true, ErrorType.Other);
                    }
                    else
                    {
                        callError(tmpBet.error,true, ErrorType.Unknown);
                    }
                }
                Stats.Balance = decimal.Parse(tmpBet.balance, System.Globalization.NumberFormatInfo.InvariantInfo) / CurCurrency.EffectiveScale;

                DiceBet newBet = new DiceBet
                {
                    TotalAmount = BetDetails.Amount,
                    DateValue = DateTime.Now,
                    Chance = BetDetails.Chance,
                    Guid = BetDetails.GUID,
                    Currency = CurrentCurrency,
                    High = BetDetails.High,
                    Nonce = tmpBet.nextNonce - 1,
                    Roll = (decimal)tmpBet.game_details.roll,
                    //UserName = username,
                    BetID = tmpBet.id,
                    ServerHash = serverseedhash,
                    ClientSeed = clientseed
                };
                bool win = false;
                if ((newBet.Roll > MaxRoll - newBet.Chance && newBet.High) || (newBet.Roll < newBet.Chance && !newBet.High))
                {
                    win = true;

                }
                if (win)
                {
                    newBet.Profit = (newBet.TotalAmount * (((100m - Edge) / newBet.Chance) - 1));

                    Stats.Wins++;
                }
                else
                {
                    newBet.Profit -= newBet.TotalAmount;
                    Stats.Losses++;
                }
                Stats.Profit += newBet.Profit;
                Stats.Wagered += newBet.TotalAmount;
                Stats.Bets++;
                callBetFinished(newBet);
                return;
            }
            catch (Exception e)
            {
                Logger.DumpLog(e);
            }
        }

        public class BKAccount
        {

            public string id { get; set; }
            public string username { get; set; }
            public string email { get; set; }

        }
        public class BKStat
        {
            public string num_bets { get; set; }
            public string wagered { get; set; }
            public string profit { get; set; }
            public int currency { get; set; }
        }
        public class bkGameDetails
        {
            public double roll { get; set; }
            public double chance { get; set; }
            public int target { get; set; }
        }

        public class bkPlaceBet
        {
            public string id { get; set; }
            public string date { get; set; }
            public string bet_amount { get; set; }
            public int currency { get; set; }
            public string profit { get; set; }
            public bkGameDetails game_details { get; set; }
            public string game_type { get; set; }
            public string balance { get; set; }
            public int nextNonce { get; set; }
            public string error { get; set; }
        }
        public class BKCurrency
        {
            public int id { get; set; }
            public string symbol { get; set; }
            public string name { get; set; }
            public int scale { get; set; }
            public string max_withdraw_limit { get; set; }
            public string min_withdraw_limit { get; set; }
            public string withdrawal_fee { get; set; }
            public string no_throttle_amount { get; set; }
            public string min_tip { get; set; }
            public string address_type { get; set; }

            public decimal EffectiveScale
            {
                get
                {
                    return (decimal)Math.Pow(10.0, (double)scale);
                }
            }
        }

        public class bkGetCurrencies
        {
            public List<BKCurrency> currencies { get; set; }
        }
        public class bkBalance
        {
            public string balance { get; set; }
            public int currency { get; set; }
        }

        public class bkGetBalances
        {
            public List<bkBalance> balances { get; set; }
        }
        public class bkLoadSTate
        {
            public string clientSeed { get; set; }
            public string serverSeedHash { get; set; }
            public int nonce { get; set; }
            public string maxWin { get; set; }
            public string minBetAmount { get; set; }
            public bool isBettingDisabled { get; set; }
        }
    }
}