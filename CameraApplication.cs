using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MvCamCtrl.NET;

namespace Hikvision_Industrial_Camera_Control_Program
{
    public partial class CameraApplication : Form
    {
        private MyCamera MY_CAMERA = new MyCamera();

        // 用于从驱动获取图像缓存
        UInt32 BUFFER_SIZE_FOR_DRIVER = 0;
        IntPtr BUFFER_FOR_DRIVER;

        // ch:用于保存图像的缓存 | en:Buffer for saving image
        UInt32 BUFFER_SIZE_FOR_IMAGE = 0;
        IntPtr BUFFER_FOR_IMAGE;

        bool CONLLECT_STATUS = false;
        private static Object BufForDriverLock = new Object();
        Thread ReceiveThread = null;


        // 设备信息列表
        MyCamera.MV_CC_DEVICE_INFO_LIST DEVICE_LIST = new MyCamera.MV_CC_DEVICE_INFO_LIST();

        // 输出帧的信息
        MyCamera.MV_FRAME_OUT_INFO_EX FRAME_INFO = new MyCamera.MV_FRAME_OUT_INFO_EX();


        public CameraApplication()
        {
            InitializeComponent();

            // 多线程程序中，新创建的线程不能访问UI线程创建的窗口控件,这时如果想要访问窗口的控件,
            // 这时可将窗口构造函数中的CheckForIllegalCrossThreadCalls设置为false；
            // 然后就能安全的访问窗体控件。
            // 尽量不要用这个，一般用
            // Invoke(new Action(() => { }));
            Control.CheckForIllegalCrossThreadCalls = false;
        }



        /// <summary>
        /// 显示错误信息
        /// </summary>
        /// <param name="csMessage"></param>
        /// <param name="nErrorNum"></param>
        private void ShowErrorMsg(string csMessage, int nErrorNum)
        {
            string errorMsg;
            if (nErrorNum == 0)
            {
                errorMsg = csMessage;
            }
            else
            {
                errorMsg = csMessage + ": Error =" + String.Format("{0:X}", nErrorNum);
            }

            switch (nErrorNum)
            {
                case MyCamera.MV_E_HANDLE: errorMsg += " Error or invalid handle "; break;
                case MyCamera.MV_E_SUPPORT: errorMsg += " Not supported function "; break;
                case MyCamera.MV_E_BUFOVER: errorMsg += " Cache is full "; break;
                case MyCamera.MV_E_CALLORDER: errorMsg += " Function calling order error "; break;
                case MyCamera.MV_E_PARAMETER: errorMsg += " Incorrect parameter "; break;
                case MyCamera.MV_E_RESOURCE: errorMsg += " Applying resource failed "; break;
                case MyCamera.MV_E_NODATA: errorMsg += " No data "; break;
                case MyCamera.MV_E_PRECONDITION: errorMsg += " Precondition error, or running environment changed "; break;
                case MyCamera.MV_E_VERSION: errorMsg += " Version mismatches "; break;
                case MyCamera.MV_E_NOENOUGH_BUF: errorMsg += " Insufficient memory "; break;
                case MyCamera.MV_E_UNKNOW: errorMsg += " Unknown error "; break;
                case MyCamera.MV_E_GC_GENERIC: errorMsg += " General error "; break;
                case MyCamera.MV_E_GC_ACCESS: errorMsg += " Node accessing condition error "; break;
                case MyCamera.MV_E_ACCESS_DENIED: errorMsg += " No permission "; break;
                case MyCamera.MV_E_BUSY: errorMsg += " Device is busy, or network disconnected "; break;
                case MyCamera.MV_E_NETER: errorMsg += " Network error "; break;
            }

            label_MessageInfo.Text = errorMsg;
        }



        /// <summary>
        /// 判断是否是彩色数据
        /// </summary>
        /// <param name="enGvspPixelType"> 像素格式 </param>
        /// <returns> 成功，返回0；错误，返回-1 </returns>
        private Boolean IsColorData(MyCamera.MvGvspPixelType enGvspPixelType)
        {
            switch (enGvspPixelType)
            {
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGR12_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerRG12_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerGB12_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_BayerBG12_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_RGB8_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_YUV422_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_YUV422_YUYV_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_YCBCR411_8_CBYYCRYY:
                    return true;

                default:
                    return false;
            }
        }


        

        /// <summary>
        /// 将active设备添加到下拉菜单里
        /// </summary>
        private void AddIntoDeviceList()
        {
            comb_DeviceList.Items.Clear();
            DEVICE_LIST.nDeviceNum = 0;
            int nRet = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref DEVICE_LIST);
            if (0 != nRet)
            {
                ShowErrorMsg("ENUMERATE DEVICES FAILED!", 0);
                return;
            }
            else
            {
                label_MessageInfo.Text = "ENUMERATE DEVICES SUCCEED";
            }
            for (int i = 0; i < DEVICE_LIST.nDeviceNum; i++)
            {
                
                MyCamera.MV_CC_DEVICE_INFO device = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(DEVICE_LIST.pDeviceInfo[i], typeof(MyCamera.MV_CC_DEVICE_INFO));
                if (device.nTLayerType == MyCamera.MV_GIGE_DEVICE)
                {
                    MyCamera.MV_GIGE_DEVICE_INFO gigeInfo = (MyCamera.MV_GIGE_DEVICE_INFO)MyCamera.ByteToStruct(device.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO));
                    if (gigeInfo.chUserDefinedName != "")
                    {
                        comb_DeviceList.Items.Add("GEV: " + gigeInfo.chUserDefinedName + " (" + gigeInfo.chSerialNumber + ")");
                    }
                    else
                    {
                        comb_DeviceList.Items.Add("GEV: " + gigeInfo.chManufacturerName + " " + gigeInfo.chModelName + " (" + gigeInfo.chSerialNumber + ")");
                    }
                }
                else if (device.nTLayerType == MyCamera.MV_USB_DEVICE)
                {
                    MyCamera.MV_USB3_DEVICE_INFO usbInfo = (MyCamera.MV_USB3_DEVICE_INFO)MyCamera.ByteToStruct(device.SpecialInfo.stUsb3VInfo, typeof(MyCamera.MV_USB3_DEVICE_INFO));
                    if (usbInfo.chUserDefinedName != "")
                    {
                        comb_DeviceList.Items.Add("U3V: " + usbInfo.chUserDefinedName + " (" + usbInfo.chSerialNumber + ")");
                    }
                    else
                    {
                        comb_DeviceList.Items.Add("U3V: " + usbInfo.chManufacturerName + " " + usbInfo.chModelName + " (" + usbInfo.chSerialNumber + ")");
                    }
                }
            }

            // ch:选择第一项 | en:Select the first item
            if (DEVICE_LIST.nDeviceNum != 0)
            {
                comb_DeviceList.SelectedIndex = 0;
            }

        }



        private void btn_ScanDevice_Click(object sender, EventArgs e)
        {
            label_MessageInfo.Text = "ENUMERATING DEVICES...";
            Application.DoEvents();
            AddIntoDeviceList();
            if (comb_DeviceList.Items.Count > 0)
            {
                btn_OpenDevice.Enabled = true;
                btn_ShutDevice.Enabled = true;
            }
            timer_MessageInfoClear.Start();
        }




        /// <summary>
        /// 启用设备
        /// </summary>
        private void OpenDevice()
        {
            if (DEVICE_LIST.nDeviceNum == 0 || comb_DeviceList.SelectedIndex == -1)
            {
                ShowErrorMsg("NO DEVICE, PLEASE SELECT!", 0);
                return;
            }

            MyCamera.MV_CC_DEVICE_INFO device = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(DEVICE_LIST.pDeviceInfo[comb_DeviceList.SelectedIndex], typeof(MyCamera.MV_CC_DEVICE_INFO));

            // ch:打开设备 | en:Open device
            if (null == MY_CAMERA)
            {
                MY_CAMERA = new MyCamera();
                if (null == MY_CAMERA) return;
            }

            // 根据输入的设备信息， 创建库内部必须的资源和初始化内部模块。 通过该接口创建设备，调用SDK接口，
            // 会默认生成SDK文件。
            int nRet = MY_CAMERA.MV_CC_CreateDevice_NET(ref device);
            if (MyCamera.MV_OK != nRet)
            {
                ShowErrorMsg("DIVICE OPEN FAILED", nRet);
                return;
            }
            // 根据设置的设备参数，找到对应的设备，连接设备
            nRet = MY_CAMERA.MV_CC_OpenDevice_NET();
            if (MyCamera.MV_OK != nRet)
            {
                MY_CAMERA.MV_CC_DestroyDevice_NET();
                ShowErrorMsg("DIVICE OPEN FAILED!", nRet);
            }
            else
            {
                label_MessageInfo.Text = "DIVICE OPEN SUCCEED";
            }
            // 探测网络最佳包大小(只对GigE相机有效)
            if (device.nTLayerType == MyCamera.MV_GIGE_DEVICE)
            {
                int nPacketSize = MY_CAMERA.MV_CC_GetOptimalPacketSize_NET();
                if (nPacketSize > 0)
                {
                    // 连接设备之后调用该接口可以设置int类型的指定节点的值。
                    nRet = MY_CAMERA.MV_CC_SetIntValueEx_NET("GevSCPSPacketSize", (uint)nPacketSize);
                    if (nRet != MyCamera.MV_OK)
                    {
                        ShowErrorMsg("SET PACKET SIZE FAILED", nRet);
                    }
                }
                else
                {
                    ShowErrorMsg("GET PACKET SIZE FAILED!", nPacketSize);
                }
            }

            // ch:设置采集连续模式 | en:Set Continues Aquisition Mode
            MY_CAMERA.MV_CC_SetEnumValue_NET("AcquisitionMode", (uint)MyCamera.MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
            MY_CAMERA.MV_CC_SetEnumValue_NET("TriggerMode", (uint)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);
            timer_getParam.Start();
            SetCtrlWhenOpen();
        }





        private Boolean IsMonoData(MyCamera.MvGvspPixelType enGvspPixelType)
        {
            switch (enGvspPixelType)
            {
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono10:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono10_Packed:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono12:
                case MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono12_Packed:
                    return true;

                default:
                    return false;
            }
        }


        private void saveImgAsBmp(string save_path)
        {
            label_MessageInfo.Text = "STARTING SAVE...";
            if (!CONLLECT_STATUS)
            {
                ShowErrorMsg("NOT START GRABBING", 0);
                return;
            }
            if (RemoveCustomPixelFormats(FRAME_INFO.enPixelType))
            {
                ShowErrorMsg("NOT SUPPORT!", 0);
                return;
            }
            IntPtr pTemp = IntPtr.Zero;
            MyCamera.MvGvspPixelType enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_Undefined;
            if (FRAME_INFO.enPixelType == MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8 || FRAME_INFO.enPixelType == MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed)
            {
                pTemp = BUFFER_FOR_DRIVER;
                enDstPixelType = FRAME_INFO.enPixelType;
            }
            else
            {
                UInt32 nSaveImageNeedSize = 0;
                MyCamera.MV_PIXEL_CONVERT_PARAM stConverPixelParam = new MyCamera.MV_PIXEL_CONVERT_PARAM();

                lock (BufForDriverLock)
                {
                    if (FRAME_INFO.nFrameLen == 0)
                    {
                        ShowErrorMsg("SAVE BMPFILE FAILED!", 0);
                        return;
                    }

                    if (IsMonoData(FRAME_INFO.enPixelType))
                    {
                        enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8;
                        nSaveImageNeedSize = (uint)FRAME_INFO.nWidth * FRAME_INFO.nHeight;
                    }
                    else if (IsColorData(FRAME_INFO.enPixelType))
                    {
                        enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed;
                        nSaveImageNeedSize = (uint)FRAME_INFO.nWidth * FRAME_INFO.nHeight * 3;
                    }
                    else
                    {
                        ShowErrorMsg("NO SUCH PIXEL TYPE!", 0);
                        return;
                    }

                    if (BUFFER_SIZE_FOR_IMAGE < nSaveImageNeedSize)
                    {
                        if (BUFFER_FOR_IMAGE != IntPtr.Zero)
                        {
                            Marshal.Release(BUFFER_FOR_IMAGE);
                        }
                        BUFFER_SIZE_FOR_IMAGE = nSaveImageNeedSize;
                        BUFFER_FOR_IMAGE = Marshal.AllocHGlobal((Int32)BUFFER_SIZE_FOR_IMAGE);
                    }

                    stConverPixelParam.nWidth = FRAME_INFO.nWidth;
                    stConverPixelParam.nHeight = FRAME_INFO.nHeight;
                    stConverPixelParam.pSrcData = BUFFER_FOR_DRIVER;
                    stConverPixelParam.nSrcDataLen = FRAME_INFO.nFrameLen;
                    stConverPixelParam.enSrcPixelType = FRAME_INFO.enPixelType;
                    stConverPixelParam.enDstPixelType = enDstPixelType;
                    stConverPixelParam.pDstBuffer = BUFFER_FOR_IMAGE;
                    stConverPixelParam.nDstBufferSize = BUFFER_SIZE_FOR_IMAGE;
                    int nRet = MY_CAMERA.MV_CC_ConvertPixelType_NET(ref stConverPixelParam);
                    if (MyCamera.MV_OK != nRet)
                    {
                        ShowErrorMsg("CONVERT PIXEL TYPE FAILED!", nRet);
                        return;
                    }
                    pTemp = BUFFER_FOR_IMAGE;
                }
            }

            lock (BufForDriverLock)
            {
                if (enDstPixelType == MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8)
                {
                    //************************Mono8 转 Bitmap*******************************
                    try
                    {
                        Bitmap bmp = new Bitmap(FRAME_INFO.nWidth, FRAME_INFO.nHeight, FRAME_INFO.nWidth * 1, PixelFormat.Format8bppIndexed, pTemp);

                        ColorPalette cp = bmp.Palette;
                        // init palette
                        for (int i = 0; i < 256; i++)
                        {
                            cp.Entries[i] = Color.FromArgb(i, i, i);
                        }
                        // set palette back
                        bmp.Palette = cp;
                        bmp.Save(save_path, ImageFormat.Bmp);
                        label_MessageInfo.Text = "SAVE BMPFILE SUCCEED";
                    }
                    catch(Exception e)
                    {
                        ShowErrorMsg("WRITE FILE FAILED!" + e.Message, 0);
                    }

                }
                else
                {
                    //*********************BGR8 转 Bitmap**************************
                    try
                    {
                        Bitmap bmp = new Bitmap(FRAME_INFO.nWidth, FRAME_INFO.nHeight, FRAME_INFO.nWidth * 3, PixelFormat.Format24bppRgb, pTemp);
                        bmp.Save(save_path, ImageFormat.Bmp);
                        label_MessageInfo.Text = "SAVE BMPFILE SUCCEED";
                    }
                    catch (Exception e)
                    {
                        ShowErrorMsg("WRITE FILE FAILED!" + e.Message, 0);
                    }
                }
            }

        }


        private void shutDevice()
        {
            
            // ch:取流标志位清零 | en:Reset flow flag bit
            if (CONLLECT_STATUS == true)
            {
                CONLLECT_STATUS = false;
                ReceiveThread.Join();
            }

            if (BUFFER_FOR_DRIVER != IntPtr.Zero)
            {
                Marshal.Release(BUFFER_FOR_DRIVER);
            }
            if (BUFFER_FOR_IMAGE != IntPtr.Zero)
            {
                Marshal.Release(BUFFER_FOR_IMAGE);
            }

            timer_getParam.Stop();
            int nRet = MY_CAMERA.MV_CC_CloseDevice_NET();
            if (MyCamera.MV_OK != nRet)
            {
                ShowErrorMsg("CLOSE DEVICE FAILED!!", nRet);
            }
            else
            {
                label_MessageInfo.Text = "CLOSE DEVICE SUCCEED";
            }
            MY_CAMERA.MV_CC_DestroyDevice_NET();
            SetCtrlWhenShut();
        }



        private void btn_OpenDevice_Click(object sender, EventArgs e)
        {
            label_MessageInfo.Text = "OPENING DEVICE...";
            Application.DoEvents();
            OpenDevice();
            timer_MessageInfoClear.Start();
        }



        private void getParam()
        {
            Application.DoEvents();
            MyCamera.MVCC_FLOATVALUE stParam = new MyCamera.MVCC_FLOATVALUE();
            int nRet = MY_CAMERA.MV_CC_GetFloatValue_NET("ExposureTime", ref stParam);

            if (MyCamera.MV_OK == nRet)
            {

                label_ExposureTime.Text = "曝光时间：" + stParam.fCurValue.ToString("F1") + "帧/秒";

                trackBar_ExposureTime.Value = (int)(Convert.ToInt32(stParam.fCurValue) / 2000);
            }

            nRet = MY_CAMERA.MV_CC_GetFloatValue_NET("Gain", ref stParam);
            if (MyCamera.MV_OK == nRet)
            {
                label_Gain.Text = "增益：" + stParam.fCurValue.ToString("F1");
                trackBar_Gain.Value = (int)(Convert.ToInt32(stParam.fCurValue) / 2);
            }

            nRet = MY_CAMERA.MV_CC_GetFloatValue_NET("ResultingFrameRate", ref stParam);

            if (MyCamera.MV_OK == nRet)
            {
                label_FrameRate.Text = "采集帧率：" + stParam.fCurValue.ToString("F1");
                trackBar_FrameRate.Value = (int)(Convert.ToInt32(stParam.fCurValue) / 1.5);
            }
        }


        
        private void trackBar_ExposureTime_Scroll(object sender, EventArgs e)
        {
            try
            {
                float.Parse((trackBar_ExposureTime.Value*2000).ToString());
            }
            catch
            {
                ShowErrorMsg("Please enter correct type!", 0);
                return;
            }
            MY_CAMERA.MV_CC_SetEnumValue_NET("ExposureAuto", 0);
            int nRet = MY_CAMERA.MV_CC_SetFloatValue_NET("ExposureTime", float.Parse((trackBar_ExposureTime.Value * 2000).ToString()));
            if (nRet != MyCamera.MV_OK)
            {
                ShowErrorMsg("Set Exposure Time Fail!", nRet);
            }
        }



        private void trackBar_FrameRate_Scroll(object sender, EventArgs e)
        {
            try
            {
                float.Parse((trackBar_FrameRate.Value * 1.5).ToString());
            }
            catch
            {
                ShowErrorMsg("Please enter correct type!", 0);
                return;
            }
            int nRet = MY_CAMERA.MV_CC_SetFloatValue_NET("AcquisitionFrameRate", float.Parse((trackBar_FrameRate.Value * 1.5).ToString()));
            if (nRet != MyCamera.MV_OK)
            {
                ShowErrorMsg("Set Frame Rate Fail!", nRet);
            }
        }



        private void trackBar_Gain_Scroll(object sender, EventArgs e)
        {
            try
            {
                float.Parse((trackBar_Gain.Value * 2).ToString());
            }
            catch
            {
                ShowErrorMsg("Please enter correct type!", 0);
                return;
            }
            MY_CAMERA.MV_CC_SetEnumValue_NET("GainAuto", 0);
            int nRet = MY_CAMERA.MV_CC_SetFloatValue_NET("Gain", float.Parse((trackBar_Gain.Value * 2).ToString()));
            if (nRet != MyCamera.MV_OK)
            {
                ShowErrorMsg("Set Gain Fail!", nRet);
            }
        }




        private void SetCtrlWhenOpen()
        {
            btn_OpenDevice.Enabled = false;
            btn_ShutDevice.Enabled = true;
            Radio_ContinuousAcquisitionMode.Enabled = true;
            Radio_ContinuousAcquisitionMode.Checked = true;
            radio_TriggerAcquisitionMode.Enabled = true;
            btn_StartCollect.Enabled = true;
            btn_StopCollect.Enabled = true;

        }



        private void SetCtrlWhenShut()
        {
            btn_OpenDevice.Enabled = true;
            btn_ShutDevice.Enabled = false;
            Radio_ContinuousAcquisitionMode.Enabled = false;
            Radio_ContinuousAcquisitionMode.Checked = false;
            radio_TriggerAcquisitionMode.Enabled = false;
            btn_StartCollect.Enabled = false;
            btn_StopCollect.Enabled = false;
        }



        private void ReceiveThreadProcess()
        {
            MyCamera.MVCC_INTVALUE stParam = new MyCamera.MVCC_INTVALUE();
            int nRet = MY_CAMERA.MV_CC_GetIntValue_NET("PayloadSize", ref stParam);
            if (MyCamera.MV_OK != nRet)
            {
                ShowErrorMsg("GET PAYLOADSIZE FAILED!", nRet);
                return;
            }
            UInt32 payLoadSize = stParam.nCurValue;
            if (payLoadSize > BUFFER_SIZE_FOR_DRIVER)
            {
                if (BUFFER_FOR_DRIVER != IntPtr.Zero)
                {
                    Marshal.Release(BUFFER_FOR_DRIVER);
                }
                BUFFER_SIZE_FOR_DRIVER = payLoadSize;
                BUFFER_FOR_DRIVER = Marshal.AllocHGlobal((Int32)BUFFER_SIZE_FOR_DRIVER);
            }
            if (BUFFER_FOR_DRIVER == IntPtr.Zero) return;
            MyCamera.MV_FRAME_OUT_INFO_EX nFRAME_INFO = new MyCamera.MV_FRAME_OUT_INFO_EX();
            MyCamera.MV_DISPLAY_FRAME_INFO nDISPLAY_INFO = new MyCamera.MV_DISPLAY_FRAME_INFO();

            while (CONLLECT_STATUS)
            {
                lock (BufForDriverLock)
                {
                    nRet = MY_CAMERA.MV_CC_GetOneFrameTimeout_NET(BUFFER_FOR_DRIVER, payLoadSize, ref nFRAME_INFO, 1000);
                    if (nRet == MyCamera.MV_OK)
                    {
                        FRAME_INFO = nFRAME_INFO;
                    }
                }
                if (nRet == MyCamera.MV_OK)
                {
                    if (RemoveCustomPixelFormats(nFRAME_INFO.enPixelType))
                    {
                        continue;
                    }

                    nDISPLAY_INFO.hWnd = pictureBox1.Handle;
                    nDISPLAY_INFO.pData = BUFFER_FOR_DRIVER;
                    nDISPLAY_INFO.nDataLen = nFRAME_INFO.nFrameLen;
                    nDISPLAY_INFO.nWidth = nFRAME_INFO.nWidth;
                    nDISPLAY_INFO.nHeight = nFRAME_INFO.nHeight;
                    nDISPLAY_INFO.enPixelType = nFRAME_INFO.enPixelType;
                    MY_CAMERA.MV_CC_DisplayOneFrame_NET(ref nDISPLAY_INFO);

                }
                else
                {
                    if (radio_TriggerAcquisitionMode.Checked)
                    {
                        Thread.Sleep(5);
                    }
                }
            }
            btn_StartCollect.Enabled = true;
        }



        // ch:去除自定义的像素格式 | en:Remove custom pixel formats
        private bool RemoveCustomPixelFormats(MyCamera.MvGvspPixelType enPixelFormat)
        {
            Int32 nResult = ((int)enPixelFormat) & (unchecked((Int32)0x80000000));
            if (0x80000000 == nResult)
            {
                return true;
            }
            else
            {
                return false;
            }
        }



        private void btn_ShutDevice_Click(object sender, EventArgs e)
        {
            label_MessageInfo.Text = "SHUTTING DOWN DEVICE...";
            Application.DoEvents();
            shutDevice();
            timer_MessageInfoClear.Start();
        }




        private void btn_StartCollect_Click(object sender, EventArgs e)
        {
            label_MessageInfo.Text = "GRABBING...";
            Application.DoEvents();
            CONLLECT_STATUS = true;
            ReceiveThread = new Thread(ReceiveThreadProcess);
            ReceiveThread.Start();
            FRAME_INFO.nFrameLen = 0; //取流之前先清除帧长度
            FRAME_INFO.enPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_Undefined;

            // ch:开始采集 | en:Start Grabbing
            int nRet = MY_CAMERA.MV_CC_StartGrabbing_NET();
            if (MyCamera.MV_OK != nRet)
            {
                CONLLECT_STATUS = false;
                ReceiveThread.Join();
                ShowErrorMsg("START GRABBING FAILED!", nRet);
                return;
            }
            else
            {
                label_MessageInfo.Text = "START GRABBING SUCCEED";
                btn_StopCollect.Enabled = true;
            }
            nRet = MY_CAMERA.MV_CC_SetGainMode_NET(2);
            timer_MessageInfoClear.Start();
        }



        private void btn_StopCollect_Click(object sender, EventArgs e)
        {
            // label_MessageInfo.Text = "STOP GRABBING...";
            // Application.DoEvents();
            CONLLECT_STATUS = false;
            ReceiveThread.Join();
            int nRet = MY_CAMERA.MV_CC_StopGrabbing_NET();
            if (nRet != MyCamera.MV_OK)
            {
                ShowErrorMsg("STOP GRABBING FAILED!", nRet);
            }
            else
            {
                label_MessageInfo.Text = "STOP GRABBING SUCCEED";
                btn_StopCollect.Enabled = false;
                Image img = Image.FromFile("black.bmp");
                pictureBox1.Image = img;
            }
            timer_MessageInfoClear.Start();
        }


        private string fileSavePath()
        {
            string path = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            string exenam = "Hikvision_Industrial_Camera_Control_Program.exe";
            path = path.Substring(0, path.Length - exenam.Length);
            path += "BitMaps\\";

            Boolean Directory_exists = Directory.Exists(path);
            if (Directory_exists)
            {
                DirectoryInfo directory = new DirectoryInfo(path);
                IEnumerable<FileInfo> enumerable = directory.EnumerateFiles();
                int files_count = enumerable.Count() + 1 ;

                if (files_count > 0 && files_count < 10)
                {
                    path += ("00000" + files_count + ".bmp");
                }
                else if (files_count >= 10 && files_count < 100)
                {
                    path += ("0000" + files_count + ".bmp");
                }
                else if (files_count >= 100 && files_count < 1000)
                {
                    path += ("000" + files_count + ".bmp");
                }
                else if (files_count >= 1000 && files_count < 10000)
                {
                    path += ("00" + files_count + ".bmp");
                }
                return path;
            }
            else
            {
                Directory.CreateDirectory(path);
                return string.Empty;
            }
        }


        private void btn_SaveAsBmp_Click(object sender, EventArgs e)
        {
            string path = fileSavePath();


            saveImgAsBmp(path);
            timer_MessageInfoClear.Start();
        }

        private void timer_MessageInfoClear_Tick(object sender, EventArgs e)
        {
            label_MessageInfo.Text = "";
            timer_MessageInfoClear.Stop();
        }


        private void timer_getParam_Tick(object sender, EventArgs e)
        {
            new Thread(() =>
            {
                getParam();
            }).Start();
        }

    }
}
