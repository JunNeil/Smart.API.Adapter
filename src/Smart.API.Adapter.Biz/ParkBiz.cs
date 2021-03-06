﻿using Smart.API.Adapter.Common;
using Smart.API.Adapter.Common.JD;
using Smart.API.Adapter.DataAccess;
using Smart.API.Adapter.Models;
using Smart.API.Adapter.Models.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Smart.API.Adapter.Biz
{
    public class ParkBiz
    {
        public  static string version = "1";
        public  static int overFlowCount = 10;
        private DataBase dataBase;
        private DataBase dataBaseDic;
        private string xmlAddr;
        private JDParkBiz jdParkBiz;

        public int HeartInterval
        {
            get
            {
                return  CommonSettings.HeartInterval;
            }
        }
        public ParkBiz()
        {
            //xmlAddr =System.IO.Directory.GetParent(System.IO.Directory.GetParent( Environment.CurrentDirectory).ToString()) + CommonSettings.ParkXmlAddress;
            dataBase = new DataBase(DataBase.DbName.SmartAPIAdapterCore, "ParkWhiteList", "VehicleNo", false);
            dataBaseDic = new DataBase(DataBase.DbName.SmartAPIAdapterCore, "ParkDic", "KeyStr", false);
            jdParkBiz = new JDParkBiz();
            InitVersion(); 
        }

        private void InitVersion()
        {
            //XML方式
            //XDocument xDoc = XDocument.Load(xmlAddr);
            //version = xDoc.Root.Element("Version").Value;
            //overFlowCount = Convert.ToInt32(xDoc.Root.Element("OverFlowCount").Value);

            try
            {
                version = dataBaseDic.FindByKey<ParkDic>("Version").ValueStr;
                overFlowCount = Convert.ToInt32(dataBaseDic.FindByKey<ParkDic>("Version").ValueStr);
            }
            catch (Exception ex)
            {
                string message = string.Format("{0}:初始化版本号出错:{1}", DateTime.Now.ToString(), ex.Message);
                if (CommonSettings.IsDev)
                {
                    Console.WriteLine(message);
                }
                LogHelper.Error(message);
            }
        }

        /// <summary>
        /// 定时执行心跳任务
        /// </summary>
        public  bool HeartCheck()
        {
            try
            {
                HeartVersion heartJd = jdParkBiz.HeartBeatCheckJd();
                
                if (heartJd.returnCode == "fail")
                {
                    //客户端未验证
                    string message = string.Format("{0}:心跳检测响应Fail:{1}", DateTime.Now.ToString(), heartJd.description);
                    if (CommonSettings.IsDev)
                    {
                        Console.WriteLine(message);
                    }
                    LogHelper.Error(message);
                    return true;
                }
                if (heartJd.returnCode == "exception")
                {
                    //服务端异常
                    string message = string.Format("{0}:心跳检测响应exception:{1}", DateTime.Now.ToString(), heartJd.description);
                    if (CommonSettings.IsDev)
                    {
                        Console.WriteLine(message);
                    }
                    LogHelper.Error(message);
                    return true;
                }
                if (heartJd.Version != ParkBiz.version)
                {                    
                    //版本号不一致需要同步白名单,获取白名单数据成功后，更新版本xml
                    if (UpdateWhiteList(ParkBiz.version))
                    {
                        ParkBiz.version = heartJd.Version;
                        ParkBiz.overFlowCount = heartJd.OverFlowCount;
                        UpdateHeartVersion(heartJd);
                    } 
                }
                return true;
            }
            catch (Exception ex)
            {
                string message = string.Format("{0}:心跳检测出错:{1}", DateTime.Now.ToString(), ex.Message);
                if (CommonSettings.IsDev)
                {
                    Console.WriteLine(message);
                }
                LogHelper.Error(message);
                return false;
            }
        }

        /// <summary>
        /// 更新白名单到本地
        /// </summary>
        public bool UpdateWhiteList(string version)
        {
            try
            {
                VehicleLegality vehicleJd = jdParkBiz.QueryVehicleLegalityJd(version);
                //服务端不可用，每隔 5s 进行重试， 5次后如仍不行， 客户端 应用 需邮件 通知 服务端 人
                //服务端处理失败,一般是校验问题
                if (vehicleJd.returnCode == "fail")
                {
                    string message = string.Format("{0}:获取白名单Fail:{1}", DateTime.Now.ToString(), vehicleJd.description);
                    if (CommonSettings.IsDev)
                    {
                        Console.WriteLine(message);
                    }
                    LogHelper.Error(message);
                    return false;
                }

                //服务端异常
                if (vehicleJd.returnCode == "exception")
                {
                    string message = string.Format("{0}:获取白名单exception:{1}", DateTime.Now.ToString(), vehicleJd.description);
                    if (CommonSettings.IsDev)
                    {
                        Console.WriteLine(message);
                    }
                    LogHelper.Error(message);
                    return false ;
                }
                //更新到数据库
                try
                {
                    foreach (VehicleInfo v in vehicleJd.data)
                    {
                        VehicleInfoDb ve = new VehicleInfoDb(v);
                        ve.UpdateTime = DateTime.Now;
                        VehicleInfoDb vehicleDb = dataBase.FindByKey<VehicleInfoDb>(v.vehicleNo);

                        if (vehicleDb!=null)
                        {
                            ve.CreateTime = vehicleDb.CreateTime;
                            dataBase.Update<VehicleInfoDb>(ve, v.vehicleNo);
                        }
                        else
                        {
                            ve.CreateTime = DateTime.Now;
                            dataBase.Insert<VehicleInfoDb>(ve);
                        }
                    }         
                    //白名单已经更新，需要重载白名单数据缓存
                    JDCommonSettings.ReLoadWhiteList();
                    return true;
                }
                catch (Exception ex)
                {
                    string message = string.Format("{0}:更新数据库出错:{1}", DateTime.Now.ToString(), ex.Message);
                    if (CommonSettings.IsDev)
                    {
                        Console.WriteLine(message);
                    }
                    LogHelper.Error(message);
                    throw ex;
                } 
            }
            catch(Exception ex)
            {
                string message = string.Format("{0}:获取京东白名单出错:{1}", DateTime.Now.ToString(), ex.Message);
                if (CommonSettings.IsDev)
                {
                    Console.WriteLine(message);
                }
                LogHelper.Error(message);
                throw ex;
            } 
        }


        /// <summary>
        /// 更新xml内容
        /// </summary>
        /// <param name="heartJd"></param>
        public void UpdateHeartVersion(HeartVersion heartJd)
        {
            try
            {
                //XDocument xDoc = XDocument.Load(xmlAddr);
                //xDoc.Root.SetElementValue("Version", heartJd.Version);
                //xDoc.Root.SetElementValue("OverFlowCount", heartJd.OverFlowCount);
                //xDoc.Save(xmlAddr);

                dataBaseDic.Update<ParkDic>(new ParkDic() { KeyStr = "Version", ValueStr = heartJd.Version }, "Version");
                dataBaseDic.Update<ParkDic>(new ParkDic() { KeyStr = "OverFlowCount", ValueStr = heartJd.OverFlowCount.ToString() }, "OverFlowCount");


            }
            catch(Exception ex)
            {
                string message = string.Format("{0}:版本更新出错:{1}", DateTime.Now.ToString(), ex.Message);
                if (CommonSettings.IsDev)
                {
                    Console.WriteLine(message);
                }
                LogHelper.Error(message);
                throw ex;
            }
        }

        /// <summary>
        /// 定时查车位总数并更新
        /// </summary>
        public async Task<bool> UpdateToltalCount()
        {
            try
            {
                TotalCountReq totalReq = new TotalCountReq();
                try
                {
                    //调用Jielink获取车场车位数据
                    ParkPlaceRes parkPlaceRes = GetParkPlaceCount();
                    if (parkPlaceRes == null)
                    {
                        //JieLink数据出错，返回true，无需发邮件。
                        return true;
                    }

                    //转换为京东车位数据
                    totalReq = new TotalCountReq();
                    totalReq.parkLotCode = CommonSettings.ParkLotCode;
                    totalReq.totalCount = parkPlaceRes.data.parkCount.ToString();
                    totalReq.data = new List<TotalInfo>();

                    parkPlaceRes.data.areaParkList.ForEach(x =>
                    {
                        totalReq.data.Add(new TotalInfo() { regionCode = x.areaNo, count = x.areaParkCount.ToString() });
                    });

                    JDCommonSettings.ParkTotalCount = parkPlaceRes.data.parkCount;
                }
                catch (Exception ex)
                {
                    string message = string.Format("{0}:获取jieLink车场数据出错:{1}", DateTime.Now.ToString(), ex.Message);
                    if (CommonSettings.IsDev)
                    {
                        Console.WriteLine(message);
                    }
                    LogHelper.Error(message);

                }

                //Demo数据
                //totalReq = new TotalCountReq();
                //totalReq.parkLotCode = CommonSettings.ParkLotCode;
                //totalReq.totalCount = "1300";
                //totalReq.data = new List<TotalInfo>();
                ////totalReq.data.Add(new TotalInfo() { regionCode = "A1", count = "100" });
                ////totalReq.data.Add(new TotalInfo() { regionCode = "A2", count = "150" });
                ////totalReq.data.Add(new TotalInfo() { regionCode = "B1", count = "200" });
                ////totalReq.data.Add(new TotalInfo() { regionCode = "B2", count = "200" });
                ////totalReq.data.Add(new TotalInfo() { regionCode = "C1", count = "200" });
                ////totalReq.data.Add(new TotalInfo() { regionCode = "C2", count = "200" });
                ////totalReq.data.Add(new TotalInfo() { regionCode = "C3", count = "250" });

                //数据推给京东
                BaseJdRes jdRes = await jdParkBiz.ModifyParkTotalCount(totalReq);
                if (jdRes.returnCode == "fail")
                {
                    string message = string.Format("{0}:更新车位总数响应Fail:{1}", DateTime.Now.ToString(), jdRes.description);
                    if (CommonSettings.IsDev)
                    {
                        Console.WriteLine(message);
                    }
                    LogHelper.Error(message);
                    //客户端未验证
                }
                if (jdRes.returnCode == "exception")
                {
                    string message = string.Format("{0}:更新车位总数响应exception:{1}", DateTime.Now.ToString(), jdRes.description);
                    if (CommonSettings.IsDev)
                    {
                        Console.WriteLine(message);
                    }
                    LogHelper.Error(message);
                    //服务端异常
                }
                return true;
            }
            catch (Exception ex)
            {
                string message = string.Format("{0}:更新车位总数出错:{1}", DateTime.Now.ToString(), ex.Message);
                if (CommonSettings.IsDev)
                {
                    Console.WriteLine(message);
                }
                LogHelper.Error(message);
                return false;

            }
        }

        /// <summary>
        /// 定时查剩余车位数并更新
        /// </summary>
        public async Task<bool> UpdateRemainCount()
        {
            try
            {
                RemainCountReq totalReq = new RemainCountReq();
                try
                {
                    //调用Jielink获取车场车位数据
                    ParkPlaceRes parkPlaceRes = GetParkPlaceCount();
                    if (parkPlaceRes == null)
                    {
                        //JieLink数据出错，返回true，无需发邮件。
                        return true;
                    }

                    //转换为京东车位数据
                    totalReq = new RemainCountReq();
                    totalReq.parkLotCode = CommonSettings.ParkLotCode;
                    totalReq.remainTotalCount = parkPlaceRes.data.parkRemainCount.ToString();
                    totalReq.data = new List<RemainInfo>();
                    parkPlaceRes.data.areaParkList.ForEach(x =>
                    {
                        totalReq.data.Add(new RemainInfo() { regionCode = x.areaNo, remainCount = x.areaParkRemainCount.ToString() });
                    });


                    JDCommonSettings.RemainTotalCount = parkPlaceRes.data.parkRemainCount;
                }
                catch (Exception ex)
                {
                    LogHelper.Error(string.Format("{0}:获取jieLink车场数据出错:{1}", DateTime.Now.ToString(), ex.Message));
                }

                //Demo数据
                //totalReq = new RemainCountReq();
                //totalReq.parkLotCode = CommonSettings.ParkLotCode;
                //totalReq.remainTotalCount = "500";
                //totalReq.data = new List<RemainInfo>();
                ////totalReq.data.Add(new RemainInfo() { regionCode = "A1", remainCount = "50" });
                ////totalReq.data.Add(new RemainInfo() { regionCode = "A2", remainCount = "100" });
                ////totalReq.data.Add(new RemainInfo() { regionCode = "B1", remainCount = "50" });
                ////totalReq.data.Add(new RemainInfo() { regionCode = "B2", remainCount = "50" });
                ////totalReq.data.Add(new RemainInfo() { regionCode = "C1", remainCount = "50" });
                ////totalReq.data.Add(new RemainInfo() { regionCode = "C2", remainCount = "100" });
                ////totalReq.data.Add(new RemainInfo() { regionCode = "C3", remainCount = "100" });

                //数据推给京东
                BaseJdRes jdRes = await jdParkBiz.ModifyParkRemainCount(totalReq);
                if (jdRes.returnCode == "fail")
                {
                    LogHelper.Error(string.Format("{0}:更新车位剩余数响应Fail:{1}", DateTime.Now.ToString(), jdRes.description));
                    //客户端未验证
                }
                if (jdRes.returnCode == "exception")
                {
                    LogHelper.Error(string.Format("{0}:更新车位剩余数响应Exception:{1}", DateTime.Now.ToString(), jdRes.description));
                    //服务端异常
                }
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(string.Format("{0}:更新车位剩余数出错:{1}", DateTime.Now.ToString(), ex.Message));
                return false;

            }
        }

        public  ParkPlaceRes GetParkPlaceCount()
        {
            //请求JieLink车场数据，parkId使用不到
            string parkId = CommonSettings.ParkLotCode; ;
            InterfaceHttpProxyApi requestApi = new InterfaceHttpProxyApi(CommonSettings.BaseAddressJS);
            var res = requestApi.PostRaw<ParkPlaceRes>("parking/place", parkId);
            if (!res.successed)
            {
                LogHelper.Error("请求JieLink出错" + res.code); 
            }
            return res.data; 
        }

        public  bool UpdateEquipmentStatus()
        {
            try
            {
                List<EquipmentStatus> equipmentStatusList = new List<EquipmentStatus>();
                //try
                //{
                //    //调用Jielink获取车场车位数据
                //    ParkPlaceRes parkPlaceRes = GetParkPlaceCount();

                //    //转换为京东车位数据
                //    totalReq = new RemainCountReq();
                //    totalReq.ParkLotCode = CommonSettings.ParkLotCode;
                //    totalReq.RemainTotalCount = parkPlaceRes.Data.ParkRemainCount;
                //    totalReq.Data = new List<RemainInfo>();

                //    parkPlaceRes.Data.AreaParkList.ForEach(x =>
                //    {
                //        totalReq.Data.Add(new RemainInfo() { RegionCode = x.AreaNo, RemainCount = x.AreaParkRemainCount });
                //    });
                //}
                //catch (Exception ex)
                //{
                //    LogHelper.Error(string.Format("{0}:获取jieLink车场数据出错:{1}", DateTime.Now.ToString(), ex.Message));
                //}

                //Demo数据
                equipmentStatusList.Add(new EquipmentStatus() { deviceGuid = "0001", deviceIoType = 1, deviceName = "闸机1", deviceStatus = "1" });
                equipmentStatusList.Add(new EquipmentStatus() { deviceGuid = "0002", deviceIoType = 2, deviceName = "闸机2", deviceStatus = "0" });
                equipmentStatusList.Add(new EquipmentStatus() { deviceGuid = "0003", deviceIoType = 1, deviceName = "闸机3", deviceStatus = "1" });
                equipmentStatusList.Add(new EquipmentStatus() { deviceGuid = "0004", deviceIoType = 3, deviceName = "闸机4", deviceStatus = "0" });

                //数据推给京东
                var jdRes = jdParkBiz.PostEquipmentStatus(equipmentStatusList);

                //if (jdRes.ReturnCode == "Fail")
                //{
                //    LogHelper.Error(string.Format("{0}:更新车位剩余数响应Fail:{1}", DateTime.Now.ToString(), jdRes.Description));
                //    //客户端未验证
                //}
                //if (jdRes.ReturnCode == "exception")
                //{
                //    LogHelper.Error(string.Format("{0}:更新车位剩余数响应Exception:{1}", DateTime.Now.ToString(), jdRes.Description));
                //    //服务端异常
                //}
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(string.Format("{0}:更新车位剩余数出错:{1}", DateTime.Now.ToString(), ex.Message));
                return false;

            }
        }










   






    }
}
