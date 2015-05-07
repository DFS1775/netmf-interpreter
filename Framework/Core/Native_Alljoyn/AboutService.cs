using System;
using System.Text;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;


namespace Microsoft.SPOT.AllJoyn
{       
    public partial class AJ
    {    
        public bool doAnnounce = false;        
        
        byte A2H(char val)
        {
            int hex = (int)val;
            if (hex >= '0' && hex <= '9') {
                return (byte)(hex - '0');
            }
            hex |= 0x20;
            if (hex >= 'a' && hex <= 'f') {
                return (byte)(10 + hex - 'a');
            } else if (hex >= 'A' && hex <= 'F') {
                return (byte)(10 + hex - 'A');
            } else {
                return 0;
            }
        }
        
        AJ_Status AJSVC_MarshalAppIdAsVariant(AJ_Message msg, string appId)
        {
            AJ_Status status = AJ_Status.AJ_OK;
            
            byte [] b = new byte[appId.Length / 2];
            for (int i = 0; i < appId.Length / 2; i ++)
            {
                b[i] = (byte)((A2H(appId[i << 1]) << 4) + (A2H(appId[(i << 1) + 1])));
            }
            
            status = MarshalArgs(msg, "v", APP_ID_SIGNATURE, b);
            
            return status;
        }                                
    
        public AJ_Status AboutPropGetter(AJ_Message msg, string language)
        {
            AJ_Status status = AJ_Status.AJ_ERR_INVALID;
            int langIndex = 0;
            AJSVC_PropertyStoreCategoryFilter filter;
            filter.bit0About = false;
            filter.bit2Announce = false;
            
            if (msg.msgId == AJ_SIGNAL_ABOUT_ANNOUNCE) {
                filter.bit2Announce = true;
                langIndex = propertyStore.GetLanguageIndex(language);
                status = AJ_Status.AJ_OK;
            } else if (msg.msgId == ((AJ_METHOD_ABOUT_GET_ABOUT_DATA) | (uint)(AJ_REP_ID_FLAG << 24)) ) {
                filter.bit0About = true;
                langIndex = propertyStore.GetLanguageIndex(language);
                status = (langIndex == PropertyStore.ERROR_LANGUAGE_INDEX) ? AJ_Status.AJ_ERR_UNKNOWN : AJ_Status.AJ_OK;
            }
            if (status == AJ_Status.AJ_OK) {
                status = PropertyStore_ReadAll(msg, filter, langIndex);
            }
            return status;
        }
        
        AJ_Status PropertyStore_ReadAll(AJ_Message msg, AJSVC_PropertyStoreCategoryFilter filter, int langIndex)
        {
            AJ_Status status = AJ_Status.AJ_OK;

            // FIXME: In order to marshal containers,
            // a native AJ_Arg struct must hang around
            // in memory because several fields in teh
            // AJ_Message struct point to the address.
            // The GetArgPtr(N) function gets the address
            // of an AJ_Arg struct defined in static
            // memory.
            
            UInt32 array = GetArgPtr(0);
            UInt32 array2 = GetArgPtr(1);
            UInt32 dict = GetArgPtr(2);
            string value = String.Empty;
            byte index;
            string ajVersion = String.Empty;
            byte fieldIndex;

            status = MarshalContainer(msg, array, (byte)AJ_Args.AJ_ARG_ARRAY);
            if (status != AJ_Status.AJ_OK) {
                return status;
            }

            for (fieldIndex = 0; fieldIndex < (byte)PropertyStoreFieldIndices.AJSVC_PROPERTY_STORE_NUMBER_OF_KEYS; fieldIndex++) {
        #if CONFIG_SERVICE
                if (propertyStore.Properties[fieldIndex].mode7Public && (filter.bit0About || (filter.bit1Config && propertyStoreProperties[fieldIndex].mode0Write) || (filter.bit2Announce && propertyStore.Properties[fieldIndex].mode1Announce))) {
        #else
                if ((propertyStore.Properties[fieldIndex].mode7Public == 1) && (filter.bit0About || (filter.bit2Announce && (propertyStore.Properties[fieldIndex].mode1Announce == 1)))) {
        #endif
                    value = propertyStore.GetValueForLang(fieldIndex, langIndex);

                    if (value == null && fieldIndex >= (byte)PropertyStoreFieldIndices.AJSVC_PROPERTY_STORE_NUMBER_OF_MANDATORY_KEYS) {     // Non existing values are skipped!
                    } else {
                        if (fieldIndex == (byte)PropertyStoreFieldIndices.AJSVC_PROPERTY_STORE_APP_ID) {
                            if (value == String.Empty) {
                                return AJ_Status.AJ_ERR_NULL;
                            }
                            status = MarshalContainer(msg, dict, (byte)AJ_Args.AJ_ARG_DICT_ENTRY);
                            if (status != AJ_Status.AJ_OK) {
                                return status;
                            }
                            status = MarshalArg(msg, "s", propertyStore.Properties[fieldIndex].keyName);
                            if (status != AJ_Status.AJ_OK) {
                                return status;
                            }
                            status = AJSVC_MarshalAppIdAsVariant(msg, value);
                            if (status != AJ_Status.AJ_OK) {
                                return status;
                            }
                            status = MarshalCloseContainer(msg, dict);
                            if (status != AJ_Status.AJ_OK) {
                                return status;
                            }
        #if CONFIG_SERVICE
                        } else if (fieldIndex == PropertyStoreFieldIndices.AJSVC_PROPERTY_STORE_MAX_LENGTH) {
                            status = AJ_MarshalArgs(msg, "{sv}", propertyStore.Properties[fieldIndex].keyName, "q", DEVICE_NAME_VALUE_LENGTH);
                            if (status != AJ_Status.AJ_OK) {
                                return status;
                            }
        #endif
                        } else if (fieldIndex == (byte)PropertyStoreFieldIndices.AJSVC_PROPERTY_STORE_AJ_SOFTWARE_VERSION) {
                            ajVersion = GetVersion();
                            if (ajVersion == String.Empty) {
                                return AJ_Status.AJ_ERR_NULL;
                            }
                            status = MarshalArgs(msg, "{sv}", propertyStore.Properties[fieldIndex].keyName, "s", ajVersion);
                            if (status != AJ_Status.AJ_OK) {
                                return status;
                            }
                        } else {
                            if (value == String.Empty) {
                                return AJ_Status.AJ_ERR_NULL;
                            }
                            status = MarshalArgs(msg, "{sv}", propertyStore.Properties[fieldIndex].keyName, "s", value);
                            if (status != AJ_Status.AJ_OK) {
                                return status;
                            }
                        }
                    }
                }
            }

            if (filter.bit0About == true) {
                // Add supported languages
                status = MarshalContainer(msg, dict, (byte)AJ_Args.AJ_ARG_DICT_ENTRY);
                if (status != AJ_Status.AJ_OK) {
                    return status;
                }
                status = MarshalArg(msg, "s", propertyStore.DefaultLanguagesKeyName);
                if (status != AJ_Status.AJ_OK) {
                    return status;
                }
                status = MarshalVariant(msg, "as");
                if (status != AJ_Status.AJ_OK) {
                    return status;
                }
                status = MarshalContainer(msg, array2, (byte)AJ_Args.AJ_ARG_ARRAY);
                if (status != AJ_Status.AJ_OK) {
                    return status;
                }

                for (index = 0; index < propertyStore.NumberOfLanguages; index++) {
                    status = MarshalArg(msg, "s", propertyStore.SupportedLanguages[index]);
                    if (status != AJ_Status.AJ_OK) {
                        return status;
                    }
                }

                status = MarshalCloseContainer(msg, array2);
                if (status != AJ_Status.AJ_OK) {
                    return status;
                }
                status = MarshalCloseContainer(msg, dict);
                if (status != AJ_Status.AJ_OK) {
                    return status;
                }
            }
            status = MarshalCloseContainer(msg, array);
            if (status != AJ_Status.AJ_OK) {
                return status;
            }

            return status;
        }
    
        public AJ_Status Initialize_AboutService()
        {
            return AJ_Status.AJ_OK;
        }        
        
        public AJ_Status AboutAnnounce(UInt32 bus, UInt32 busAboutPort)
        {
            AJ_Status status = AJ_Status.AJ_OK;
            AJ_Message announcement = new AJ_Message();

            if (!doAnnounce) {
                return status;
            }
            doAnnounce = false;
            if (busAboutPort == 0) {
                return status;
            }

            status = MarshalSignal(bus, announcement, AJ_SIGNAL_ABOUT_ANNOUNCE, 0, 0, (byte)ALLJOYN_FLAG_SESSIONLESS, 0);
            if (status != AJ_Status.AJ_OK) {
                goto ErrorExit;
            }
            status = MarshalArg(announcement, "q", ABOUT_VERSION);
            if (status != AJ_Status.AJ_OK) {
                goto ErrorExit;
            }
            status = MarshalArg(announcement, "q", busAboutPort);
            if (status != AJ_Status.AJ_OK) {
                goto ErrorExit;
            }
            status = MarshalObjectDescriptions(announcement);
            if (status != AJ_Status.AJ_OK) {
                goto ErrorExit;
            }
            
            if (propertyStore != null) {
                //status = propStoreGetterCB(announcement, "", this);
                AboutPropGetter(announcement, "");
            } else {
                status = MarshalDefaultProps(announcement);
            }
            
            if (status != AJ_Status.AJ_OK) {
                goto ErrorExit;
            }
            
            return DeliverMsg(announcement);

        ErrorExit:
            return status;
        }
        
        static AJ_Status AboutGetPropCB(AJ_Message reply, AJ_Message msg, uint propId, AJ aj)
        {
            if (propId == AJ_PROPERTY_ABOUT_VERSION) {
                return aj.MarshalArg(reply, "q", ABOUT_VERSION);
            } else {
                return AJ_Status.AJ_ERR_UNEXPECTED;
            }
        }
        
        public AJ_Status AboutHandleGetProp(AJ_Message msg)
        {
            return BusGetProp(msg, AboutGetPropCB);
        }
        
        static AJ_Status AboutIconGetPropCB(AJ_Message reply, AJ_Message msg, uint propId, AJ aj)
        {
            AJ_Status status = AJ_Status.AJ_ERR_UNEXPECTED;

            if (propId == AJ_PROPERTY_ABOUT_ICON_VERSION_PROP) {
                status = aj.MarshalArg(reply, "q", ABOUT_ICON_VERSION);
            } else if (propId == AJ_PROPERTY_ABOUT_ICON_MIMETYPE_PROP) {
                status = aj.MarshalArg(reply, "s", aj.AboutIconMime);
            } else if (propId == AJ_PROPERTY_ABOUT_ICON_SIZE_PROP) {
                status = aj.MarshalArg(reply, "u", aj.AboutIconSize);
            }
            return status;
        }
        
        public AJ_Status AboutIconHandleGetProp(AJ_Message msg)
        {
            return BusGetProp(msg, AboutIconGetPropCB);
        }
        
        AJ_Status AboutIconHandleGetURL(AJ_Message msg, AJ_Message reply)
        {
            AJ_Status status;

            status = MarshalReplyMsg(msg, reply);
            if (status != AJ_Status.AJ_OK) {
                goto ErrorExit;
            }
            return MarshalArg(reply, "s", AboutIconURL);

        ErrorExit:
            return status;
        }
        
        public AJ_Status AboutHandleGetAboutData(AJ_Message msg, AJ_Message reply)
        {
            AJ_Status status = AJ_Status.AJ_OK;
            string language = String.Empty;

            language = UnmarshalArgs(msg, "s");
            if (status == AJ_Status.AJ_OK) {
                MarshalReplyMsg(msg, reply);
                if (propertyStore != null) {
                    status = AboutPropGetter(reply, language);
                } else {
                    status = MarshalDefaultProps(reply);
                }
                if (status != AJ_Status.AJ_OK) {
                    //status = AJ_MarshalErrorMsg(msg, reply, AJ_ErrLanguageNotSuppored);
                }
            }
            return status;
        }
        
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern AJ_Status MarshalObjectDescriptions(AJ_Message msg);              
    
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern AJ_Status MarshalDefaultProps(AJ_Message msg);
        
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern UInt32 GetArgPtr(int idx);
        
        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern string GetArgString(int idx);

    }
}