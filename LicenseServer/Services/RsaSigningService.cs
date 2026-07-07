using System;
using System.Security.Cryptography;

namespace LicenseServer.Services
{
    public interface IRsaSigningService
    {
        string SignData(string data);
        string GetPublicKeyXml();
    }

    public class RsaSigningService : IRsaSigningService
    {
        private const string PrivateKeyXml = @"<RSAKeyValue><Modulus>ojILooC76YJpMh60RLTCKsgoxexHbG0fKZ0qt1TV32MEu3dDh8DWnuoVL8m6ZcJrS2GOG9837mqc/G435R2mO/+WwgvBysK93kZzrjZZ4iVBaRQ6VXsTeYd+Aj8WawQTkOhDujKC77qYMs2DETbsdp8GGcyf02NTzgV4C43SsYD2CTKdD5IgJ2okUBOIAq2G8lsqS294w6J0hLuelmfOLPd53mHZPX0ufnipU7jwJQp38zljvL5Sjp4QvrjJc3EWAVrunC3hR1LKZs7BLUbjdZWCyLSiBBaSsErW8pSU6KSEC9vjsgfrsCG5bpIvI8WkLelHmyc4uVFprb5InR5J9Q==</Modulus><Exponent>AQAB</Exponent><P>1m2OoV3dRn76zYN/ljGYVaIwQGh8+irMAgQJMqR5RIoFFUThFR6zyQTUNvWp18/o/5qb4AUA93mdDIO5Hay1+EemuwVnH3Sd/qxh4OvLaV0ppHvGGTN8Aqe84XQ3GHieAlsGFTzIuBshv8+fceB78RFt7lB+SH8vban6y4kmGgM=</P><Q>waQY+cNj3d8LTYWk4FXbUR05SI1KOQonOFGrOGILMJYUjXM7gjKPC8oBGdc2niu5ZmKySvryXAGotiP5qQR4W0/0dKai3CWEFySmmzkT8llinIRREkYQMCjt2fdTd6AMOsr0nCSK3qWX5Av37T/P9bvk3xhXe5S6HGcI1Ae3xqc=</Q><DP>vLiP39YM+g6oDli94iKkQDoO3aEY3dTs2JlUvw2i7X/MGXwV3dC3yyRE4lo0sYx7NPuOVQwSXbzbTDhipIttXKczR0bqC/VHWO2+94aP8JveGrYVE/kMHAolYwg1tYPzDX+vSuHEhsTaX0cMvd0lOHZummCdxJCr3YjNAnYi4qE=</DP><DQ>rANCcHRCPXCKENY8LU/3X+nO3gUsvtinGF9r8s0dVY6sOS742OJisb1DFxpXmVAMBMh9yx96tYJ/xTTV7W9cHvk6lXkFSPxGh2x2V4LvliQS9iiP/+SfMrjY+Pu8eJKC6qMpgZ7wgXGmKNz84xMBgC/l0sxDwjLO1LYuYHNurBc=</DQ><InverseQ>GozJPBzLJ4hRGaINY3BwgTbd2gn1Ltf9+nXVfvie5jIYEfmzlE05yjhnr1XbRfziPAe1/rqNHdV1SoQFvYLtGPVn7xk82a5jmbwkPHad/YaT25bheaJ3ypilZmHTNI3mF+K7cJyqr48YCm1+yXDRIDGoZe2/O0UgtR/kEfvykaQ=</InverseQ><D>Ree36AP/+XaBjF57Z5lYjkPSfuuFJRAq/C6G+JkRzMPKiFmwu1O7rKZLF1ukgLM4tzaGnzCn1JQSsSF36cHLodRYz61tisxANQq8VPuL5dIUzQsw0SLIk/p3rtQt/1W0cSIJ/rhCgrwzWMIGmWbIp5+Ga5wrzlnjBsqIoMIxatrwTv6UiHhS7Mu2y+HB8RBStuI5nMd3ozWWqCo8aOmdudPrJM14yi2j9fU69HNcw3fdNdZ5j7UWFupaTcYDbmKDlDxWOP3fwbNaQatAmYy1jvqYZbSoILHe/GzvMFZ7GIBQCkfy/aGbccu0cLK08KqJmpWAeStuUNPiJpS0w1kHiQ==</D></RSAKeyValue>";

        private const string PublicKeyXml = @"<RSAKeyValue><Modulus>ojILooC76YJpMh60RLTCKsgoxexHbG0fKZ0qt1TV32MEu3dDh8DWnuoVL8m6ZcJrS2GOG9837mqc/G435R2mO/+WwgvBysK93kZzrjZZ4iVBaRQ6VXsTeYd+Aj8WawQTkOhDujKC77qYMs2DETbsdp8GGcyf02NTzgV4C43SsYD2CTKdD5IgJ2okUBOIAq2G8lsqS294w6J0hLuelmfOLPd53mHZPX0ufnipU7jwJQp38zljvL5Sjp4QvrjJc3EWAVrunC3hR1LKZs7BLUbjdZWCyLSiBBaSsErW8pSU6KSEC9vjsgfrsCG5bpIvI8WkLelHmyc4uVFprb5InR5J9Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        public string SignData(string data)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.FromXmlString(PrivateKeyXml);
                var dataBytes = System.Text.Encoding.UTF8.GetBytes(data);
                var signatureBytes = rsa.SignData(dataBytes, CryptoConfig.MapNameToOID("SHA256")!);
                return Convert.ToBase64String(signatureBytes);
            }
        }

        public string GetPublicKeyXml()
        {
            return PublicKeyXml;
        }
    }
}
