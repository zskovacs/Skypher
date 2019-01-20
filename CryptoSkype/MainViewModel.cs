using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SKYPE4COMLib;

namespace CryptoSkype
{
    public class MainViewModel : BindableBase
    {
        private Skype _skype;

        public ObservableCollection<Chat> ChatList { get; set; }

        public MainViewModel()
        {
            _skype = new Skype();
            _skype.Attach(7, false);

            _skype.MessageStatus += SkypeMessageStatus;
            
            SendMessageCommand = new DelegateCommand(SendMessage, o => true);
            VanishCommand = new DelegateCommand(Vanish, () => true);
            RefreshChats = new DelegateCommand(GetChats, () => true);

            ChatList = new ObservableCollection<Chat>();
            GetChats();            
        }

        private void SendMessage(object obj)
        {
            if (!string.IsNullOrEmpty(Message))
            {
                string encryptedMessage;
                try
                {
                    encryptedMessage = Encrypt(Message);
                }
                catch (Exception)
                {
                    encryptedMessage = Message;
                }
                AppendChatLine("You", Message);
                ActiveChat.SendMessage(encryptedMessage);
                Message = string.Empty;
            }
        }

        private void Vanish()
        {
            Message = string.Empty;
            Chat = string.Empty;
        }

        private void SkypeMessageStatus(ChatMessage pMessage, TChatMessageStatus status)
        {
            if (ActiveChat == null || status != TChatMessageStatus.cmsReceived || ActiveChat.Name != pMessage.Chat.Name)
                return;

            string decryptedMessage;
            try
            {
                decryptedMessage = Decrypt(pMessage.Body);
            }
            catch (Exception)
            {
                decryptedMessage = pMessage.Body;
            }
            pMessage.Seen = true;
            AppendChatLine(pMessage.Sender.FullName, decryptedMessage);
            pMessage.Chat.ClearRecentMessages();
        }

        private void GetChats()
        {
            ChatList.Clear();
            foreach (Chat aChat in _skype.ActiveChats)
            {
                if(!string.IsNullOrEmpty(aChat.Name))
                    ChatList.Add(aChat);
            }

        }

        private void AppendChatLine(string name, string message)
        {
            Chat += string.Format("[{0}]: {1} {2}", name, message, Environment.NewLine);
        }

        public DelegateCommand SendMessageCommand { get; set; }
        public DelegateCommand RefreshChats { get; set; }
        public DelegateCommand VanishCommand { get; set; }

        private string _chat;

        public string Chat
        {
            get { return _chat; }
            set { SetProperty(ref _chat, value); }
        }


        private string _message;

        public string Message
        {
            get { return _message; }
            set { SetProperty(ref _message, value); }
        }

        private string _secret;

        public string Secret
        {
            get { return _secret; }
            set
            {
                SetProperty(ref _secret, value);
                InitializeAes(_secret);
            }
        }

        private Chat _activeChat;

        public Chat ActiveChat
        {
            get { return _activeChat; }
            set { SetProperty(ref _activeChat, value); }
        }

        public string _activeChatName;

        public string ActiveChatName
        {
            get { return _activeChatName; }
            set
            {
                foreach (Chat aChat in _skype.ActiveChats)
                {
                    if (aChat.Name == value)
                    {
                        ActiveChat = aChat;
                        _activeChatName = value;
                    }
                }
            }
        }

        #region AES
        private AesCryptoServiceProvider aesProvider;

        private void InitializeAes(string passphrase)
        {
            if (string.IsNullOrEmpty(passphrase))
                return;

            var encoding = new UTF8Encoding();
            var key = encoding.GetBytes(passphrase);

            var md5Provider = new MD5CryptoServiceProvider();
            var hashedKey = md5Provider.ComputeHash(key);

            aesProvider = new AesCryptoServiceProvider
            {
                Key = hashedKey,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.PKCS7
            };

            md5Provider.Clear();

        }

        private string Encrypt(string input)
        {
            if (aesProvider == null)
            {
                return string.Empty;
            }

            var encryptor = aesProvider.CreateEncryptor(aesProvider.Key, aesProvider.IV);

            using (var msEncrypt = new MemoryStream())
            {
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (var swEncrypt = new StreamWriter(csEncrypt))
                    {

                        //Write all data to the stream.
                        swEncrypt.Write(input);
                    }
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        private string Decrypt(string input)
        {
            if (aesProvider == null)
            {
                return string.Empty;
            }

            var decryptor = aesProvider.CreateDecryptor(aesProvider.Key, aesProvider.IV);

            using (var msDecrypt = new MemoryStream(Convert.FromBase64String(input)))
            {
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (var srDecrypt = new StreamReader(csDecrypt))
                    {

                        // Read the decrypted bytes from the decrypting stream 
                        // and place them in a string.
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
        }
        #endregion
    }
}
