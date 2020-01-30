﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using NodaTime;
using NUnit.Framework;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Data.Market;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.HistoricalData;
using QuantConnect.Tests.Engine.DataFeeds;
using HistoryRequest = QuantConnect.Data.HistoryRequest;

namespace QuantConnect.Tests.Algorithm
{
    public class AlgorithmHistoryTests
    {
        private QCAlgorithm _algorithm;
        private TestHistoryProvider _testHistoryProvider;

        [SetUp]
        public void Setup()
        {
            _algorithm = new QCAlgorithm();
            _algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(_algorithm));
            _algorithm.HistoryProvider = _testHistoryProvider = new TestHistoryProvider();
        }

        [Test]
        [TestCase(Resolution.Tick)]
        [TestCase(Resolution.Second)]
        [TestCase(Resolution.Minute)]
        [TestCase(Resolution.Hour)]
        [TestCase(Resolution.Daily)]
        public void TimeSpanHistoryRequestIsCorrectlyBuilt(Resolution resolution)
        {
            _algorithm.SetStartDate(2013, 10, 07);
            _algorithm.History(Symbols.SPY, TimeSpan.FromSeconds(2), resolution);
            Resolution? fillForwardResolution = null;
            if (resolution != Resolution.Tick)
            {
                fillForwardResolution = resolution;
            }
            Assert.AreEqual(1, _testHistoryProvider.HistryRequests.Count);
            Assert.AreEqual(Symbols.SPY, _testHistoryProvider.HistryRequests.First().Symbol);
            Assert.AreEqual(resolution, _testHistoryProvider.HistryRequests.First().Resolution);
            Assert.IsFalse(_testHistoryProvider.HistryRequests.First().IncludeExtendedMarketHours);
            Assert.IsFalse(_testHistoryProvider.HistryRequests.First().IsCustomData);
            Assert.AreEqual(fillForwardResolution, _testHistoryProvider.HistryRequests.First().FillForwardResolution);
            Assert.AreEqual(DataNormalizationMode.Adjusted, _testHistoryProvider.HistryRequests.First().DataNormalizationMode);
            Assert.AreEqual(TickType.Trade, _testHistoryProvider.HistryRequests.First().TickType);
        }

        [Test]
        [TestCase(Resolution.Tick)]
        [TestCase(Resolution.Second)]
        [TestCase(Resolution.Minute)]
        [TestCase(Resolution.Hour)]
        [TestCase(Resolution.Daily)]
        public void BarCountHistoryRequestIsCorrectlyBuilt(Resolution resolution)
        {
            _algorithm.SetStartDate(2013, 10, 07);
            _algorithm.History(Symbols.SPY, 10, resolution);
            Resolution? fillForwardResolution = null;
            if (resolution != Resolution.Tick)
            {
                fillForwardResolution = resolution;
            }
            Assert.AreEqual(1, _testHistoryProvider.HistryRequests.Count);
            Assert.AreEqual(Symbols.SPY, _testHistoryProvider.HistryRequests.First().Symbol);
            Assert.AreEqual(resolution, _testHistoryProvider.HistryRequests.First().Resolution);
            Assert.IsFalse(_testHistoryProvider.HistryRequests.First().IncludeExtendedMarketHours);
            Assert.IsFalse(_testHistoryProvider.HistryRequests.First().IsCustomData);
            Assert.AreEqual(fillForwardResolution, _testHistoryProvider.HistryRequests.First().FillForwardResolution);
            Assert.AreEqual(DataNormalizationMode.Adjusted, _testHistoryProvider.HistryRequests.First().DataNormalizationMode);
            Assert.AreEqual(TickType.Trade, _testHistoryProvider.HistryRequests.First().TickType);
        }

        [Test]
        public void TickHistoryRequestIgnoresFillForward()
        {
            _algorithm.SetStartDate(2013, 10, 07);
            _algorithm.History(new [] {Symbols.SPY}, new DateTime(1,1,1,1,1,1), new DateTime(1, 1, 1, 1, 1, 2), Resolution.Tick, fillForward: true);

            Assert.AreEqual(1, _testHistoryProvider.HistryRequests.Count);
            Assert.AreEqual(Symbols.SPY, _testHistoryProvider.HistryRequests.First().Symbol);
            Assert.AreEqual(Resolution.Tick, _testHistoryProvider.HistryRequests.First().Resolution);
            Assert.IsFalse(_testHistoryProvider.HistryRequests.First().IncludeExtendedMarketHours);
            Assert.IsFalse(_testHistoryProvider.HistryRequests.First().IsCustomData);
            Assert.AreEqual(null, _testHistoryProvider.HistryRequests.First().FillForwardResolution);
            Assert.AreEqual(DataNormalizationMode.Adjusted, _testHistoryProvider.HistryRequests.First().DataNormalizationMode);
            Assert.AreEqual(TickType.Trade, _testHistoryProvider.HistryRequests.First().TickType);
        }

        [Test]
        public void GetLastKnownPriceOfIliquidAsset_RealData()
        {
            var algorithm = new QCAlgorithm();
            algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
            algorithm.HistoryProvider = new SubscriptionDataReaderHistoryProvider();
            algorithm.HistoryProvider.Initialize(new HistoryProviderInitializeParameters(
                null,
                null,
                new DefaultDataProvider(),
                new ZipDataCacheProvider(new DefaultDataProvider()),
                new LocalDiskMapFileProvider(),
                new LocalDiskFactorFileProvider(),
                null,
                false));

            algorithm.SetDateTime(new DateTime(2014, 6, 6, 15, 0, 0));

            //20140606_twx_minute_quote_american_call_230000_20150117.csv
            var optionSymbol = Symbol.CreateOption("TWX", Market.USA, OptionStyle.American, OptionRight.Call, 23, new DateTime(2015,1,17));
            var option = algorithm.AddOptionContract(optionSymbol);

            var lastKnownPrice = algorithm.GetLastKnownPrice(option);
            Assert.IsNotNull(lastKnownPrice);

            // Data gap of more than 15 minutes
            Assert.Greater((algorithm.Time - lastKnownPrice.EndTime).TotalMinutes, 15);
        }


        [Test]
        public void GetLastKnownPriceOfIliquidAsset_TestData()
        {
            // Set the start date on Tuesday
            _algorithm.SetStartDate(2014, 6, 10);

            var optionSymbol = Symbol.CreateOption("TWX", Market.USA, OptionStyle.American, OptionRight.Call, 23, new DateTime(2015, 1, 17));
            var option = _algorithm.AddOptionContract(optionSymbol);

            // The last known price is on Friday, so we missed data from Monday and no data during Weekend
            var barTime = new DateTime(2014, 6, 6, 15, 0, 0, 0);
            _testHistoryProvider.Slices = new[] 
            { 
                new Slice(barTime, new[] { new TradeBar(barTime, optionSymbol, 100, 100, 100, 100, 1) })
            }.ToList();

            var lastKnownPrice = _algorithm.GetLastKnownPrice(option);
            Assert.IsNotNull(lastKnownPrice);
            Assert.AreEqual(barTime.AddMinutes(1), lastKnownPrice.EndTime);
        }

        private class TestHistoryProvider : HistoryProviderBase
        {
            public override int DataPointCount { get; }
            public List<HistoryRequest> HistryRequests { get; } = new List<HistoryRequest>();

            public List<Slice> Slices { get; set; } = new List<Slice>();

            public override void Initialize(HistoryProviderInitializeParameters parameters)
            {
                throw new NotImplementedException();
            }

            public override IEnumerable<Slice> GetHistory(IEnumerable<HistoryRequest> requests, DateTimeZone sliceTimeZone)
            {
                foreach (var request in requests)
                {
                    HistryRequests.Add(request);
                }

                var startTime = requests.Min(x => x.StartTimeUtc.ConvertFromUtc(x.DataTimeZone));
                var endTime = requests.Max(x => x.EndTimeUtc.ConvertFromUtc(x.DataTimeZone));

                return Slices.Where(x => x.Time >= startTime && x.Time <= endTime).ToList();
            }
        }
    }
}
