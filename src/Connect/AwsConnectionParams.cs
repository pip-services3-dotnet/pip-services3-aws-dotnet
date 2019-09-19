using System.Collections.Generic;
using PipServices3.Commons.Config;
using PipServices3.Commons.Data;
using PipServices3.Commons.Errors;
using PipServices3.Components.Auth;
using PipServices3.Components.Connect;

namespace PipServices3.Aws.Connect
{
    /// <summary>
    /// Contains connection parameters to authenticate against Amazon Web Services (AWS)
    /// and connect to specific AWS resource.
    /// 
    /// The class is able to compose and parse AWS resource ARNs.
    /// 
    /// ### Configuration parameters ###
    /// 
    /// - access_id:     application access id
    /// - client_id:     alternative to access_id
    /// - access_key:    application secret key
    /// - client_key:    alternative to access_key
    /// - secret_key:    alternative to access_key
    /// 
    /// In addition to standard parameters CredentialParams may contain any number of custom parameters
    /// </summary>
    /// <example>
    /// <code>
    /// var connection = AwsConnectionParams.FromTuples(
    /// "region", "us-east-1",
    /// "access_id", "XXXXXXXXXXXXXXX",
    /// "secret_key", "XXXXXXXXXXXXXXX",
    /// "service", "s3",
    /// "bucket", "mybucket" );
    /// 
    /// var region = connection.Region;                     // Result: "us-east-1"
    /// var accessId = connection.AccessId;                 // Result: "XXXXXXXXXXXXXXX"
    /// var secretKey = connection.AccessKey;               // Result: "XXXXXXXXXXXXXXX"
    /// var pin = connection.GetAsNullableString("bucket");      // Result: "mybucket"
    /// </code>
    /// </example>
    /// See <see cref="AwsConnectionResolver"/>
    public class AwsConnectionParams : ConnectionParams
    {
        /// <summary>
        /// Creates an new instance of the connection parameters.
        /// </summary>
        public AwsConnectionParams()
        { }

        /// <summary>
        /// Creates an new instance of the connection parameters.
        /// </summary>
        /// <param name="map">(optional) an object to be converted into key-value pairs to initialize this connection.</param>
        public AwsConnectionParams(IDictionary<string, string> map)
            : base(map)
        { }

        /// <summary>
        /// Creates an new instance of the connection parameters.
        /// </summary>
        /// <param name="connection">connection parameters</param>
        /// <param name="credential">credential parameters</param>
        public AwsConnectionParams(ConnectionParams connection, CredentialParams credential)
        {
            if (connection != null)
                Append(connection);
            if (credential != null)
                Append(credential);
        }

        /// <summary>
        /// Gets or sets the AWS partition name.
        /// </summary>
        public string Partition
        {
            get { return GetAsNullableString("partition") ?? "aws"; }
            set { base["partition"] = value; }
        }

        /// <summary>
        /// Gets or sets the AWS service name.
        /// </summary>
        public string Service
        {
            get { return GetAsNullableString("service") ?? GetAsNullableString("protocol"); }
            set { base["service"] = value; }
        }

        /// <summary>
        /// Gets or sets the AWS region.
        /// </summary>
        public string Region
        {
            get { return GetAsNullableString("region"); }
            set { base["region"] = value; }
        }

        /// <summary>
        /// Gets or sets the AWS account id.
        /// </summary>
        public string Account
        {
            get { return GetAsNullableString("account"); }
            set { base["account"] = value; }
        }

        /// <summary>
        /// Gets or sets the AWS resource type.
        /// </summary>
        public string ResourceType
        {
            get { return GetAsNullableString("resource_type"); }
            set { base["resource_type"] = value; }
        }

        /// <summary>
        /// Gets or sets the AWS resource id.
        /// </summary>
        public string Resource
        {
            get { return GetAsNullableString("resource"); }
            set { base["resource"] = value; }
        }

        /// <summary>
        /// Gets or sets the AWS resource ARN.
        /// </summary>
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

        /// <summary>
        /// Gets or sets the AWS access id.
        /// </summary>
        public string AccessId
        {
            get { return GetAsNullableString("access_id") ?? GetAsNullableString("client_id"); }
            set { base["access_id"] = value; }
        }

        /// <summary>
        /// Gets or sets the AWS client key.
        /// </summary>
        public string AccessKey
        {
            get {
                var accessKey = GetAsNullableString("access_key");
                accessKey = accessKey ?? GetAsNullableString("client_key");
                accessKey = accessKey ?? GetAsNullableString("secret_key");
                return accessKey;
            }
            set { base["access_key"] = value; }
        }

        /// <summary>
        /// Creates a new AwsConnectionParams object filled with key-value pairs serialized as a string.
        /// </summary>
        /// <param name="line">a string with serialized key-value pairs as "key1=value1;key2=value2;..."
        /// Example: "Key1=123;Key2=ABC;Key3=2016-09-16T00:00:00.00Z"</param>
        /// <returns>a new AwsConnectionParams object.</returns>
        public static new AwsConnectionParams FromString(string line)
        {
            var map = StringValueMap.FromString(line);
            return new AwsConnectionParams(map);
        }

        /// <summary>
        /// Validates this connection parameters 
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <returns>a ConfigException or null if validation passed successfully.</returns>
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

        /// <summary>
        /// Retrieves AwsConnectionParams from configuration parameters.
        /// The values are retrieves from "connection" and "credential" sections.
        /// </summary>
        /// <param name="config">configuration parameters</param>
        /// <returns>the generated AwsConnectionParams object.</returns>
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

        /// <summary>
        /// Retrieves AwsConnectionParams from multiple configuration parameters.
        /// The values are retrieves from "connection" and "credential" sections.
        /// </summary>
        /// <param name="configs">a list with configuration parameters</param>
        /// <returns>the generated AwsConnectionParams object.</returns>
        public static AwsConnectionParams MergeConfigs(params ConfigParams[] configs)
        {
            var config = ConfigParams.MergeConfigs(configs);
            return new AwsConnectionParams(config);
        }
    }
}
