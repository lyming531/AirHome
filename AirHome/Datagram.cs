﻿using System;
using System.Collections.Generic;
using System.Text;

namespace ThisCoder.AirHome
{
    /// <summary>
    /// 报文结构体
    /// </summary>
    public struct Datagram
    {
        /// <summary>
        /// 起始符
        ///     <para>只读属性</para>
        ///     <para>值为0X02</para>
        /// </summary>
        public Byte Stx
        {
            get
            {
                return 0X02;
            }
            private set { }
        }

        /// <summary>
        /// 消息头
        ///     <para>长度为12字节</para>
        /// </summary>
        public MessageHead Head { get; set; }

        /// <summary>
        /// 消息体
        ///     <para>长度可变</para>
        /// </summary>
        public MessageBody Body { get; set; }

        /// <summary>
        /// 结束符
        ///     <para>只读属性</para>
        ///     <para>值为0X03</para>
        /// </summary>
        public Byte Etx
        {
            get
            {
                return 0X03;
            }
            private set { }
        }

        /// <summary>
        /// 通过消息头和消息体初始化报文对象实例
        /// </summary>
        /// <param name="head">
        /// 消息头
        ///     <para>长度为12字节</para>
        /// </param>
        /// <param name="body">
        /// 消息体
        ///     <para>长度可变</para>
        /// </param>
        public Datagram(MessageHead head, MessageBody body)
            : this()
        {
            Head = head;
            Body = body;
        }

        /// <summary>
        /// 获取消息报文字节数组
        /// </summary>
        /// <returns></returns>
        public Byte[] GetDatagram()
        {
            List<Byte> dg = new List<byte> { this.Stx };

            Byte[] head = this.Head.GetHead();
            Byte[] body = this.Body.GetBody();

            dg.AddRange(Escaping(head));
            dg.AddRange(Escaping(body));

            dg.Add(this.Etx);

            return dg.ToArray();
        }

        /// <summary>
        /// 获取消息报文对象列表
        /// </summary>
        /// <param name="dataArray">消息报文字节数组</param>
        /// <returns></returns>
        public static List<Datagram> GetDatagramList(Byte[] dataArray)
        {
            if (dataArray.Length < 22)
            {
                throw new AirException("命令格式错误。", ResponseCode.CommandFormatError);
            }

            List<Byte[]> byteArrayList = new List<byte[]>();
            List<Byte[]> newByteArrayList = new List<byte[]>();

            GetByteArrayList(dataArray, 0, ref byteArrayList);
            Descaping(byteArrayList, ref newByteArrayList);

            MessageHead mh = new MessageHead();
            MessageBody mb = new MessageBody();
            Datagram d = new Datagram();
            List<Datagram> datagramList = new List<Datagram>();

            foreach (var byteArray in newByteArrayList)
            {
                if (!Enum.IsDefined(typeof(MessageType), byteArray[0]))
                {
                    throw new AirException("不支持该类型的命令。", ResponseCode.NonsupportType);
                }

                mh.Type = (MessageType)byteArray[0];
                mh.Length = (UInt16)((byteArray[1] << 8) + byteArray[2]);
                mh.SeqNumber = (UInt32)((byteArray[3] << 24) + (byteArray[4] << 16) + (byteArray[5] << 8) + byteArray[6]);
                mh.Reserved = (UInt32)((byteArray[7] << 16) + (byteArray[8] << 8) + byteArray[9]);
                mh.Crc = (UInt16)((byteArray[10] << 8) + byteArray[11]);

                if (!Enum.IsDefined(typeof(MessageId), (UInt16)((byteArray[12] << 8) + byteArray[13])))
                {
                    throw new AirException("不支持该操作。", ResponseCode.NonsupportOperation);
                }

                mb.MsgId = (MessageId)((byteArray[12] << 8) + byteArray[13]);
                mb.DevId = ((UInt64)byteArray[14] << 56) + ((UInt64)byteArray[15] << 48) + ((UInt64)byteArray[16] << 40) + ((UInt64)byteArray[17] << 32)
                    + ((UInt64)byteArray[18] << 24) + ((UInt64)byteArray[19] << 16) + ((UInt64)byteArray[20] << 8) + byteArray[21];

                List<Parameter> pmtList = new List<Parameter>();
                GetParameterList(byteArray, 22, ref pmtList);

                if (pmtList.Count > 0)
                {
                    mb.PmtList = pmtList;

                    if (Crc.GetCrc(mb.GetBody()) != mh.Crc)
                    {
                        throw new AirException("消息体CRC校验错误。", ResponseCode.CrcCheckError);
                    }
                }
                else
                {
                    throw new AirException("参数格式错误。", ResponseCode.ParameterFormatError);
                }

                d.Head = mh;
                d.Body = mb;
                datagramList.Add(d);
            }

            return datagramList;
        }

        /// <summary>
        /// 转义特殊字符
        ///     <para>STX转义为ESC和0XE7，即02->1BE7</para>
        ///     <para>ETX转义为ESC和0XE8，即03->1BE8</para>
        ///     <para>ESC转义为ESC和0X00，即1B->1B00</para>
        /// </summary>
        /// <param name="byteArray">消息报文字节数组</param>
        /// <returns></returns>
        private static Byte[] Escaping(Byte[] byteArray)
        {
            List<Byte> byteList = new List<byte>();

            foreach (var item in byteArray)
            {
                if (item == 0X02)
                {
                    byteList.Add(0X1B);
                    byteList.Add(0XE7);
                }
                else if (item == 0X03)
                {
                    byteList.Add(0X1B);
                    byteList.Add(0XE8);
                }
                else if (item == 0X1B)
                {
                    byteList.Add(0X1B);
                    byteList.Add(0X00);
                }
                else
                {
                    byteList.Add(item);
                }
            }

            return byteList.ToArray();
        }

        /// <summary>
        /// 去除转义特殊字符
        /// </summary>
        /// <param name="byteArrayList">原消息报文字节数组列表</param>
        /// <param name="newByteArrayList">新消息报文字节数组列表</param>
        private static void Descaping(List<Byte[]> byteArrayList, ref List<Byte[]> newByteArrayList)
        {
            List<Byte> byteList;

            foreach (var item in byteArrayList)
            {
                byteList = new List<Byte>();

                for (int i = 0; i < item.Length; i++)
                {
                    if (item[i] == 0X1B)
                    {
                        if (i + 1 < item.Length)
                        {
                            switch (item[i + 1])
                            {
                                case 0XE7:
                                    byteList.Add(0X02);
                                    break;
                                case 0XE8:
                                    byteList.Add(0X03);
                                    break;
                                case 0X00:
                                    byteList.Add(0X1B);
                                    break;
                                default:
                                    byteList.Add(item[i + 1]);
                                    break;
                            }
                        }
                        else
                        {
                            break;
                        }

                        i++;
                    }
                    else
                    {
                        byteList.Add(item[i]);
                    }
                }

                newByteArrayList.Add(byteList.ToArray());
            }
        }

        /// <summary>
        /// 获取参数对象列表
        /// </summary>
        /// <param name="byteArray">消息报文字节数组</param>
        /// <param name="index">数组索引</param>
        /// <param name="pmtList">参数对象列表</param>
        private static void GetParameterList(Byte[] byteArray, int index, ref List<Parameter> pmtList)
        {
            if (byteArray.Length > index + 2)
            {
                Parameter parameter = new Parameter(); ;
                List<Byte> byteList = new List<Byte>();

                parameter.Type = (ParameterType)((byteArray[index] << 8) + byteArray[index + 1]);
                parameter.Length = byteArray[index + 2];

                for (int j = index + 3; j < index + parameter.Length + 3; j++)
                {
                    byteList.Add(byteArray[j]);
                }

                parameter.Value = byteList;
                pmtList.Add(parameter);

                GetParameterList(byteArray, index + parameter.Length + 3, ref pmtList);
            }
        }

        /// <summary>
        /// 获取消息报文字节数组列表
        ///     <para>此列表中的消息报文字节数组不包含起止符。</para>
        /// </summary>
        /// <param name="dataArray">消息报文字节数组</param>
        /// <param name="index">数组索引</param>
        /// <param name="byteArrayList">消息报文字节数组列表</param>
        private static void GetByteArrayList(Byte[] dataArray, int index, ref List<Byte[]> byteArrayList)
        {
            bool isStx = false;
            List<Byte> byteList = new List<Byte>();

            for (int i = index; i < dataArray.Length; i++)
            {
                if (dataArray[i] == 0X02)
                {
                    byteList = new List<Byte>();
                    isStx = true;
                }
                else if (dataArray[i] == 0X03)
                {
                    isStx = false;

                    if (byteList.Count > 0)
                    {
                        byteArrayList.Add(byteList.ToArray());
                    }

                    GetByteArrayList(dataArray, i + 1, ref byteArrayList);
                    break;
                }
                else if (isStx)
                {
                    byteList.Add(dataArray[i]);
                }
            }
        }

        /// <summary>
        /// 获取消息报文十六进制字符串
        /// </summary>
        /// <param name="separator">
        /// 分隔符
        ///     <para>默认为空字符</para>
        /// </param>
        /// <returns></returns>
        public string ToHexString(string separator = "")
        {
            StringBuilder sb = new StringBuilder();

            foreach (var item in this.GetDatagram())
            {
                sb.Append(item.ToString("X2") + separator);
            }

            return sb.ToString().TrimEnd(separator.ToCharArray());
        }

        /// <summary>
        /// 获取消息报文字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Encoding.UTF8.GetString(this.GetDatagram());
        }
    }

    /// <summary>
    /// 消息头结构体
    /// </summary>
    public struct MessageHead
    {
        /// <summary>
        /// 消息类型
        ///     <para>Byte类型，长度为1个字节</para>
        /// </summary>
        public MessageType Type { get; set; }

        /// <summary>
        /// 消息体长度
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </summary>
        public UInt16 Length { get; set; }

        /// <summary>
        /// 消息序号
        ///     <para>UInt32类型，长度为4个字节</para>
        /// </summary>
        public UInt32 SeqNumber { get; set; }

        /// <summary>
        /// 预留字段
        ///     <para>UInt32类型，长度为3字节</para>
        /// </summary>
        public UInt32 Reserved { get; set; }

        /// <summary>
        /// 消息体CRC校验
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </summary>
        public UInt16 Crc { get; set; }

        /// <summary>
        /// 通过“消息类型”初始化消息头对象实例
        /// </summary>
        /// <param name="type">
        /// 消息类型
        ///     <para>Byte类型，长度为1个字节</para>
        /// </param>
        public MessageHead(MessageType type)
            : this()
        {
            Type = type;
        }

        /// <summary>
        /// 通过“消息体长度”、“消息序号”和“消息体CRC校验”初始化消息头对象实例
        ///     <para>默认消息类型为服务器到设备</para>
        /// </summary>
        /// <param name="length">
        /// 消息体长度
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </param>
        /// <param name="seqNumber">
        /// 消息序号
        ///     <para>UInt32类型，长度为4个字节</para>
        /// </param>
        /// <param name="crc">
        /// 消息体CRC校验
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </param>
        public MessageHead(UInt16 length, UInt32 seqNumber, UInt16 crc)
            : this()
        {
            Type = MessageType.ServerToDevice;
            Length = length;
            SeqNumber = seqNumber;
            Crc = crc;
        }

        /// <summary>
        /// 通过“消息体长度”、“消息类型”、“消息序号”和“消息体CRC校验”初始化消息头对象实例
        /// </summary>
        /// <param name="type">
        /// 消息类型
        ///     <para>Byte类型，长度为1个字节</para>
        /// </param>
        /// <param name="length">
        /// 消息体长度
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </param>
        /// <param name="seqNumber">
        /// 消息序号
        ///     <para>UInt32类型，长度为4个字节</para>
        /// </param>
        /// <param name="crc">
        /// 消息体CRC校验
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </param>
        public MessageHead(MessageType type, UInt16 length, UInt32 seqNumber, UInt16 crc)
            : this()
        {
            Type = type;
            Length = length;
            SeqNumber = seqNumber;
            Crc = crc;
        }

        /// <summary>
        /// 获取消息头字节数组
        /// </summary>
        /// <returns></returns>
        public Byte[] GetHead()
        {
            List<Byte> mh = new List<byte>();
            mh.Add((Byte)(this.Type));
            mh.Add((Byte)(this.Length >> 8));
            mh.Add((Byte)(this.Length));

            for (int i = 24; i >= 0; i -= 8)
            {
                mh.Add((Byte)(this.SeqNumber >> i));
            }

            for (int j = 0; j < 3; j++)
            {
                mh.Add(0X00);
            }

            mh.Add((Byte)(this.Crc >> 8));
            mh.Add((Byte)(this.Crc));

            return mh.ToArray();
        }

        /// <summary>
        /// 获取消息头十六进制字符串
        /// </summary>
        /// <param name="separator">
        /// 分隔符
        ///     <para>默认为空字符</para>
        /// </param>
        /// <returns></returns>
        public string ToHexString(string separator = "")
        {
            StringBuilder sb = new StringBuilder();

            foreach (var item in this.GetHead())
            {
                sb.Append(item.ToString("X2") + separator);
            }

            return sb.ToString().TrimEnd(separator.ToCharArray());
        }

        /// <summary>
        /// 获取消息头字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Encoding.UTF8.GetString(this.GetHead());
        }
    }

    /// <summary>
    /// 消息体结构体
    /// </summary>
    public struct MessageBody
    {
        /// <summary>
        /// 消息ID
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </summary>
        public MessageId MsgId { get; set; }

        /// <summary>
        /// 设备ID
        ///     <para>UInt64类型，长度为8个字节</para>
        /// </summary>
        public UInt64 DevId { get; set; }

        /// <summary>
        /// 参数列表
        ///     <para>长度可变</para>
        /// </summary>
        public List<Parameter> PmtList { get; set; }

        /// <summary>
        /// 通过“消息ID”初始化消息体对象实例
        ///     <para>默认设备ID为0X0000000000000000，即广播到所有设备</para>
        /// </summary>
        /// <param name="msgId">
        /// 消息ID
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </param>
        public MessageBody(MessageId msgId)
            : this()
        {
            MsgId = msgId;
            DevId = 0X0000000000000000;
        }

        /// <summary>
        /// 通过“设备ID”初始化消息体对象实例
        ///     <para>默认消息ID为0X0000，即包含多个功能</para>
        /// </summary>
        /// <param name="devId">
        /// 设备ID
        ///     <para>UInt64类型，长度为8个字节</para>
        /// </param>
        public MessageBody(UInt64 devId)
            : this()
        {
            MsgId = MessageId.Multifunction;
            DevId = devId;
        }

        /// <summary>
        /// 通过“消息ID”和“设备ID”初始化消息体对象实例
        /// </summary>
        /// <param name="msgId">
        /// 消息ID
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </param>
        /// <param name="devId">
        /// 设备ID
        ///     <para>UInt64类型，长度为8个字节</para>
        /// </param>
        public MessageBody(MessageId msgId, UInt64 devId)
            : this()
        {
            MsgId = msgId;
            DevId = devId;
        }

        /// <summary>
        /// 通过“消息ID”、“设备ID”和“参数列表”初始化消息体对象实例
        /// </summary>
        /// <param name="msgId">
        /// 消息ID
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </param>
        /// <param name="devId">
        /// 设备ID
        ///     <para>UInt64类型，长度为8个字节</para>
        /// </param>
        /// <param name="pmtList">
        /// 参数列表
        ///     <para>长度可变</para>
        /// </param>
        public MessageBody(MessageId msgId, UInt64 devId, List<Parameter> pmtList)
            : this()
        {
            MsgId = msgId;
            DevId = devId;
            PmtList = pmtList;
        }

        /// <summary>
        /// 获取消息体字节数组
        /// </summary>
        /// <returns></returns>
        public Byte[] GetBody()
        {
            List<Byte> mb = new List<byte>{
                (Byte)((UInt16)(this.MsgId) >> 8),
                (Byte)(this.MsgId)
            };

            for (int i = 56; i >= 0; i -= 8)
            {
                mb.Add((Byte)(this.DevId >> i));
            }

            foreach (var pmt in this.PmtList ?? new List<Parameter>())
            {
                mb.AddRange(pmt.GetParameter());
            }

            return mb.ToArray();
        }

        /// <summary>
        /// 获取消息体十六进制字符串
        /// </summary>
        /// <param name="separator">
        /// 分隔符
        ///     <para>默认为空字符</para>
        /// </param>
        /// <returns></returns>
        public string ToHexString(string separator = "")
        {
            StringBuilder sb = new StringBuilder();

            foreach (var item in this.GetBody())
            {
                sb.Append(item.ToString("X2") + separator);
            }

            return sb.ToString().TrimEnd(separator.ToCharArray());
        }

        /// <summary>
        /// 获取消息体字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Encoding.UTF8.GetString(this.GetBody());
        }
    }

    /// <summary>
    /// 参数结构体
    /// </summary>
    public struct Parameter
    {
        /// <summary>
        /// 参数类型
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </summary>
        public ParameterType Type { get; set; }

        /// <summary>
        /// 参数值长度
        ///     <para>Byte类型，长度为1个字节</para>
        /// </summary>
        public Byte Length { get; set; }

        /// <summary>
        /// 参数值字节列表
        ///     <para>Byte类型列表，长度可变</para>
        /// </summary>
        public List<Byte> Value { get; set; }

        /// <summary>
        /// 通过“参数类型”和字节类型的“参数值”初始化参数对象实例
        /// </summary>
        /// <param name="type">
        /// 参数类型
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </param>
        /// <param name="byteValue">Byte类型的参数值</param>
        public Parameter(ParameterType type, Byte byteValue)
            : this()
        {
            Type = type;
            Length = 0X01;
            Value = new List<byte> { byteValue };
        }

        /// <summary>
        /// 通过“参数类型”和字节数组类型的“参数值”初始化参数对象实例
        /// </summary>
        /// <param name="type">
        /// 参数类型
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </param>
        /// <param name="byteArrayValue">字节数组类型的参数值</param>
        public Parameter(ParameterType type, Byte[] byteArrayValue)
            : this()
        {
            Type = type;
            Length = (Byte)byteArrayValue.Length;
            Value = new List<byte>(byteArrayValue);
        }

        /// <summary>
        /// 通过“参数类型”和字符串类型的“参数值”初始化参数对象实例
        /// </summary>
        /// <param name="type">
        /// 参数类型
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </param>
        /// <param name="stringValue">字符串类型的参数值</param>
        public Parameter(ParameterType type, string stringValue)
            : this()
        {
            List<Byte> byteValueList = new List<byte>(Encoding.UTF8.GetBytes(stringValue));

            Type = type;
            Length = (Byte)byteValueList.Count;
            Value = byteValueList;
        }

        /// <summary>
        /// 通过“参数类型”和“参数值字节列表”初始化参数对象实例
        /// </summary>
        /// <param name="type">
        /// 参数类型
        ///     <para>UInt16类型，长度为2个字节</para>
        /// </param>
        /// <param name="byteValueList">
        /// 参数值字节列表
        ///     <para>Byte类型列表，长度可变</para>
        /// </param>
        public Parameter(ParameterType type, List<Byte> byteValueList)
            : this()
        {
            Type = type;
            Length = (Byte)byteValueList.Count;
            Value = byteValueList;
        }

        /// <summary>
        /// 获取参数字节数组
        /// </summary>
        /// <returns></returns>
        public Byte[] GetParameter()
        {
            List<Byte> pmt = new List<byte> {
                (Byte)((UInt16)(this.Type) >> 8),
                (Byte)(this.Type),
                this.Length
            };

            if (this.Value != null && this.Value.Count > 0)
            {
                pmt.AddRange(this.Value);
            }
            else
            {
                pmt.Add(0X00);
            }

            return pmt.ToArray();
        }
    }
}