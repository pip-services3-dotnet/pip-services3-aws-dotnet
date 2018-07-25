using System.Collections.Generic;
using PipServices.Commons.Config;
using PipServices.Commons.Data;
using PipServices.Commons.Errors;
using PipServices.Components.Auth;
using PipServices.Components.Connect;

namespace PipServices.Aws.Connect
{
    public class AwsConnectionParams : ConnectionParams
    {
        public AwsConnectionParams()
        { }

        public AwsConnectionParams(IDictionary<string, string> map)
            : base(map)
        { }

        public AwsConnectionParams(ConnectionParams connection, CredentialParams credential)
        {
            if (connection != null)
                Append(connection);
            if (credential != null)
                Append(credential);
        }

        public string Partition
        {
            get { return GetAsNullableString("partition") ?? "aws"; }
            set { base["partition"] = value; }
        }

        public string Service
        {
            get { return GetAsNullableString("service") ?? GetAsNullableString("protocol"); }
            set { base["service"] = value; }
        }

        public string Region
        {
            get { return GetAsNullableString("region"); }
            set { base["region"] = value; }
        }

        public string Account
        {
            get { return GetAsNullableString("account"); }
            set { base["account"] = value; }
        }

        public string ResourceType
        {
            get { return GetAsNullableString("resource_type"); }
            set { base["resource_type"] = value; }
        }

        public string Resource
        {
            get { return GetAsNullableString("resource"); }
            set { base["resource"] = value; }
        }

        public string Arn
        {
            get
            {
                string arn = GetAsNullableString("arn");
                if (arn != null) return arn;

                arn = "arn";
                string partition = Partition ?? "aws";
                arn += ":" + partition;
                string service = Service ?? "";
                arn += ":" + service;
                string region = Region ?? "";
                arn += ":" + region;
                string account = Account ?? "";
                arn += ":" + account;
                string resourceType = ResourceType ?? "";
                if (resourceType != "")
                    arn += ":" + resourceType;
                string resource = Resource ?? "";
                arn += ":" + resource;

                return arn;
            }
            set
            {
                base["arn"] = value;

                if (value != null)
                {
                    string[] tokens = value.Split(':');
                    Partition = tokens.Length > 1 ? tokens[1] : null;
                    Service = tokens.Length > 2 ? tokens[2] : null;
                    Region = tokens.Length > 3 ? tokens[3] : null;
                    Account = tokens.Length > 5 ? tokens[4] : null;
                    if (tokens.Length > 6)
                    {
                        ResourceType = tokens[5];
                        Resource = tokens[6];
                    }
                    else
                    {
                        string temp = tokens[5];
                        int pos = temp.IndexOf("/");
                        if (pos > 0)
                        {
                            ResourceType = temp.Substring(0, pos);
                            Resource = temp.Substring(pos + 1);
                        }
                        else
                        {
                            ResourceType = null;
                            Resource = temp;
                        }
                    }
                }
            }
        }

        public string AccessId
        {
            get { return GetAsNullableString("access_id") ?? GetAsNullableString("client_id"); }
            set { base["access_id"] = value; }
        }

        public string AccessKey
        {
            get { return GetAsNullableString("access_key") ?? GetAsNullableString("client_key"); }
            set { base["access_key"] = value; }
        }

        public static new AwsConnectionParams FromString(string line)
        {
            var map = StringValueMap.FromString(line);
            return new AwsConnectionParams(map);
        }

        public ConfigException Validate(string correlationId)
        {
            string arn = Arn;
            if (arn == "arn:aws::::")
            {
                return new ConfigException(
                    correlationId,
                    "NO_AWS_CONNECTION",
                    "AWS connection is not set"
                );
            }

            if (AccessId == null)
            {
                return new ConfigException(
                    correlationId,
                    "NO_ACCESS_ID",
                    "No access_id is configured in AWS credential"
                );
            }

            if (AccessKey == null)
            {
                return new ConfigException(
                    correlationId,
                    "NO_ACCESS_KEY",
                    "No access_key is configured in AWS credential"
                );
            }

            return null;
        }

        public static AwsConnectionParams FromConfig(ConfigParams config)
        {
            var result = new AwsConnectionParams();

            var credentials = CredentialParams.ManyFromConfig(config);
            foreach (var credential in credentials)
                result.Append(credential);

            var connections = ConnectionParams.ManyFromConfig(config);
            foreach (var connection in connections)
                result.Append(connection);

            return result;
        }

        public static AwsConnectionParams MergeConfigs(params ConfigParams[] configs)
        {
            var config = ConfigParams.MergeConfigs(configs);
            return new AwsConnectionParams(config);
        }
    }
}
