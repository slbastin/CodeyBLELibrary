﻿using System;
using System.Linq;
using Windows.Foundation;
using Windows.Storage.Streams;
using BluetoothLE;
using BluetoothLE.Models;

namespace CodeyBLELibrary
{
    public class VariableValueChangedArgs : EventArgs
    {
        public SharedVariable Variable { set; get; }
    }

    public class MessageReceivedArgs : EventArgs
    {
        public BroadcastMessage Message { set; get; }
    }

    /// <summary>
    /// 程小奔BLE连接工具类
    /// </summary>
    public class Codey
    {
        /// <summary>
        /// 暂时使用单例模式控制只能连接一个设备
        /// </summary>
        protected Codey()
        {
        }
        public static Codey Instance { private set; get; } = new Codey();

        /// <summary>
        /// 成功连接程小奔设备的通知
        /// </summary>
        public event TypedEventHandler<Codey, EventArgs> CodeyConnected;
        
        /// <summary>
        /// 共享变量更改通知
        /// </summary>
        public event TypedEventHandler<Codey, VariableValueChangedArgs> VariableValueChanged;

        /// <summary>
        /// 广播的消息接收事件
        /// </summary>
        public event TypedEventHandler<Codey, MessageReceivedArgs> MessageReceived;

        /// <summary>
        /// 枚举设备，发现Makeblock设备后自动连接
        /// </summary>
        /// <returns></returns>
        public void Connect()
        {
            EnumDevices();
        }

        /// <summary>
        /// 设置共享变量的值
        /// </summary>
        /// <param name="var"></param>
        public void SetSharedVariable(SharedVariable var)
        {
            WriteDataAsync(var.ToPacket());
        }

        /// <summary>
        /// 发送广播消息给程小奔
        /// </summary>
        /// <param name="message"></param>
        public void SendBroadcastMessage(BroadcastMessage message)
        {
            WriteDataAsync(message.ToPacket());
        }

        /// <summary>
        /// 枚举蓝牙设备
        /// </summary>
        private void EnumDevices()
        {
            Context.DeviceEnumEvent += Context_DeviceEnumEvent;
            Context.StartEnumeration();
        }

        /// <summary>
        /// 设备枚举通知，判断可连接设备。
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void Context_DeviceEnumEvent(GattSampleContext sender, BLTEEnumEventArgs args)
        {
            if (args.Device.Name.Contains(FilterName))
            {
                if (args.Notify == BLTEEnumEvent.Add)
                {
                    if (SelectedDevice == null)
                    {
                        SelectedDevice = args.Device;
                        Context.StopEnumeration();
                        await SelectedDevice.Connect();
                        GetCharacteristics();
                        OnCodeyConnected();
                    }
                }
                else
                {
                    SelectedDevice = null;
                }
            }
        }

        /// <summary>
        /// 获取codey的收发端口
        /// </summary>
        private void GetCharacteristics()
        {
            foreach (var service in SelectedDevice.Services)
            {
                WriteCharacteristics = service.Characteristics.FirstOrDefault(c => c.UUID.Equals(UUID_TX));
                ReadCharacteristics = service.Characteristics.FirstOrDefault(c => c.UUID.Equals(UUID_RX));

                if (WriteCharacteristics != null && ReadCharacteristics != null)
                {
                    ReadCharacteristics.ValueChanged += ReadCharacteristics_ValueChanged;
                    break;
                }
            }
        }

        private void ReadCharacteristics_ValueChanged(ObservableGattCharacteristics sender, CharacteristicsValueChangedArgs args)
        {
            foreach (var t in args.Data)
            {
                if (_parser.PushData(t))
                {
                    var result = CodeyShareableFactory.Generate(_parser.GetPacket());

                    if (result is SharedVariable variable)
                    {
                        OnVariableValueChanged(new VariableValueChangedArgs()
                        {
                            Variable = variable 
                        });
                    }else if (result is BroadcastMessage message)
                    {
                        OnMessageReceived(new MessageReceivedArgs()
                        {
                            Message = message
                        });
                    }
                }
            }
        }



        private void WriteDataAsync(byte[] data)
        {
           WriteCharacteristics.WriteValueAsync(data);
        }


        protected virtual void OnCodeyConnected()
        {
            CodeyConnected?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnVariableValueChanged(VariableValueChangedArgs args)
        {
            VariableValueChanged?.Invoke(this, args);
        }

        protected virtual void OnMessageReceived(MessageReceivedArgs args)
        {
            MessageReceived?.Invoke(this, args);
        }


        private GattSampleContext Context => GattSampleContext.Context;
        private ObservableBluetoothLEDevice SelectedDevice { set; get; } = null;
        private ObservableGattCharacteristics WriteCharacteristics { set; get; } = null;
        private ObservableGattCharacteristics ReadCharacteristics { set; get; } = null;
        public const string FilterName = "Makeblock";

        private readonly CodeyProtocolParser _parser = new CodeyProtocolParser();

        private const string UUID_RX = "0000ffe2-0000-1000-8000-00805f9b34fb";
        private const string UUID_TX = "0000ffe3-0000-1000-8000-00805f9b34fb";

    }


}