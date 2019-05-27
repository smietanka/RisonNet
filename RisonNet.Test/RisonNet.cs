using System;
using System.Collections.Generic;
using System.Dynamic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RisonNet.Test
{
    [TestClass]
    public class RisonNet
    {
        [TestMethod]
        public void TestMethod1()
        {
            var rison = new Rison();

            // #1 -- sprawdza kompleksowy obiekt
            var firstTest = rison.DecodeToObject("(name:!n,author:/writer~,comments:!('great','not.so.great'))");
            Assert.AreEqual(null, firstTest.name);
            Assert.AreEqual("/writer~", firstTest.author);
            CollectionAssert.AreEqual(new dynamic[] { "great", "not.so.great" }, firstTest.comments);
            // #2 -- sprawdza obiekty z liczbami
            var secondTest = rison.DecodeToObject("(i:1,j:2)");
            Assert.AreEqual(1, secondTest.i);
            Assert.AreEqual(2, secondTest.j);

            // #3 -- sprawdza czy zamiana w te i spowrotem da ten sam efekt
            var ObjToTest = "(Filter:(Attributes:!((Key:'111',Value:!('1010','888'))),MatchedFilter:(PriceDifference:(IsLower:!t,IsMin:!t,PerCentDifference:!n,ValueDifference:!n),Statuses:!(1,2,3,4,5,6,7,8,9,10,13,99)),PriceSource:-1,Statuses:!(2,7,9,99)),Tab:'dashboard')";
            var dynamicObj = rison.DecodeToObject(ObjToTest);
            string encodeDynamicObj = rison.Encode(dynamicObj);
            Assert.AreEqual(ObjToTest, encodeDynamicObj);

            // #4 -- sprawdza czy da rade zamienic dynamiczny obiekt ExpandoObject na rison i sprawdza przy okazji czy zamiana odwrotna da ten sam wynik
            dynamic dynObjToPut = new ExpandoObject();
            dynObjToPut.Filter = new {
                ProductName = "test",
                Statuses = new List<int>() { 11, 12 },
                PriceSource = 1,
                MatchedFilter = new {
                    Statuses = new List<int>() { 10, 0, 1, 2 }
                }
            };
            dynObjToPut.Tab = "dashboard";
            var encodedDynObj = rison.Encode(dynObjToPut);
            var objFromExpando = rison.DecodeToObject(encodedDynObj);
            Assert.AreEqual(objFromExpando.Filter.ProductName, dynObjToPut.Filter.ProductName);
            Assert.AreEqual(objFromExpando.Filter.PriceSource, dynObjToPut.Filter.PriceSource);
            CollectionAssert.AreEqual(objFromExpando.Filter.Statuses, dynObjToPut.Filter.Statuses);
            CollectionAssert.AreEqual(objFromExpando.Filter.MatchedFilter.Statuses, dynObjToPut.Filter.MatchedFilter.Statuses);
            Assert.AreEqual(objFromExpando.Tab, dynObjToPut.Tab);
        }
    }
}
