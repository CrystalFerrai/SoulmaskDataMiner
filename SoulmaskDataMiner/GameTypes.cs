// Copyright 2024 Crystal Ferrai
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace SoulmaskDataMiner
{
	/// <summary>
	/// "Big achievement" categories
	/// </summary>
	internal enum EChengJiuBigType
	{
		ECHBIGTYPE_NOT_DEFINE,
		ECHBIGTYPE_CHENGZHANG,
		ECHBIGTYPE_MAP_TANSUO,
		ECHBIGTYPE_KILL_TARGET,
		ECHBIGTYPE_MANREN_MANAGER,
		ECHBIGTYPE_SUB_CHENGJIU,
		ECHBIGTYPE_ZHIZAO,
		ECHBIGTYPE_JIESUO_MIANJIA,
		ECHBIGTYPE_MAX,
	};

	/// <summary>
	/// Achievement categories
	/// </summary>
	internal enum EChengJiuType
	{
		ECHT_NOT_DEFINE,
		ECHT_JIHUO_SHIBEI,
		ECHT_MIANJU_LEVEL,
		ECHT_KILL_TARGET,
		ECHT_COLLET_SHIBEI,
		ECHT_UNLOCK_MAP_MIWU,
		ECHT_FOUND_TANSUODIAN,
		ECHT_MANREN_ZHENSHE,
		ECHT_MANREN_ZHAOMU,
		ECHT_MANREN_KONGZHI,
		ECHT_MANREN_ZHIHUI,
		ECHT_MANREN_SIWANG,
		ECHT_MANREN_JINGYING,
		ECHT_ZHIZAO_PEIFANG,
		ECHT_JIESUO_MIANJU,
		ECHT_MAX,
	};

	/// <summary>
	/// Tirbal status of human NPC
	/// </summary>
	internal enum EClanDiWei
	{
		CLAN_DIWEI_LOW,
		CLAN_DIWEI_MIDDLE,
		CLAN_DIWEI_HIGH,
		CLAN_DIWEI_MAX
	}

	/// <summary>
	/// Occupation of human NPC
	/// </summary>
	internal enum EClanZhiYe
	{
		ZHIYE_TYPE_NONE,
		ZHIYE_TYPE_WUWEI,
		ZHIYE_TYPE_SHOULIE,
		ZHIYE_TYPE_SHOUHU,
		ZHIYE_TYPE_KULI,
		ZHIYE_TYPE_ZAGONG,
		ZHIYE_TYPE_ZONGJIANG,
		ZHIYE_TYPE_ZHIZHE,
		ZHIYE_TYPE_XIULIAN,
		ZHIYE_TYPE_JISHI,
		ZHIYE_TYPE_MAX
	}

	/// <summary>
	/// Clan membership of human NPC
	/// </summary>
	internal enum EClanType
	{
		CLAN_TYPE_NONE,
		CLAN_TYPE_A,
		CLAN_TYPE_B,
		CLAN_TYPE_C,
		CLAN_TYPE_D,
		CLAN_TYPE_E,
		CLAN_TYPE_F,
		CLAN_TYPE_MAX,
		CLAN_TYPE_INVADER // Not part of original enum
	}

	/// <summary>
	/// Item category
	/// </summary>
	internal enum EDaoJuCaiLiaoType
	{
		EDJCL_QiTa,
		EDJCL_ZhiWu,
		EDJCL_KuangWu,
		EDJCL_DongWu,
		EDJCL_WuQi,
		EDJCL_FangJu,
		EDJCL_GongJu,
		EDJCL_QiMin,
		EDJCL_JiaJu,
		EDJCL_JianZhu,
		EDJCL_ShiCai,
		EDJCL_YaoWu,
		EDJCL_BanChenPin,
		EDJCL_LiaoLi,
		EDJCL_Max,
	}


	/// <summary>
	/// Item quality
	/// </summary>
	internal enum EDaoJuPinZhi
	{
		EDJPZ_Level1,
		EDJPZ_Level2,
		EDJPZ_Level3,
		EDJPZ_Level4,
		EDJPZ_Level5,
		EDJPZ_Level6,
		EDJPZ_Max
	}

	/// <summary>
	/// Tool suggestion for foliage collection
	/// </summary>
	internal enum EFoliageCollectSuggestToolType
	{
		EFCSTT_SuggestUse,
		EFCSTT_NotSuggestUse,
		EFCSTT_SuggestLowest,
		EFCSTT_MAX,
	};

	/// <summary>
	/// Gameplay settings category
	/// </summary>
	internal enum EGameXiShuType
	{
		/// <summary>
		/// General
		/// </summary>
		EGXST_TONGYONG,

		/// <summary>
		/// EXP & Growth
		/// </summary>
		EGXST_JINGYAN,

		/// <summary>
		/// Output & Drops
		/// </summary>
		EGXST_CHANCHU,

		/// <summary>
		/// Building
		/// </summary>
		EGXST_JIANZHU,

		/// <summary>
		/// Refresh
		/// </summary>
		EGXST_SHUAXIN,

		/// <summary>
		/// Combat
		/// </summary>
		EGXST_ZHANDOU,

		/// <summary>
		/// Consumption
		/// </summary>
		EGXST_XIAOHAO,

		/// <summary>
		/// Invasion
		/// </summary>
		EGXST_RUQIN,

		/// <summary>
		/// PvP Time
		/// </summary>
		EGXST_PVPTIME,

		/// <summary>
		/// AI Settings
		/// </summary>
		EGXST_AI,

		/// <summary>
		/// Battle Time
		/// </summary>
		EGXST_WARTIME,

		EGXST_MAX
	};

	/// <summary>
	/// Tattoo location
	/// </summary>
	internal enum EHWenShenBuWei
	{
		Min,
		Tou,   // Head
		Xiong, // Chest
		Shou,  // Arm
		Jiao,  // Leg
		Max
	};

	/// <summary>
	/// Game function type (buildings/structures)
	/// </summary>
	internal enum EJianZhuGameFunctionType
	{
		EJZGFT_NOT_DEFINE,
		EJZGFT_SUMMON_NPC,
		EJZGFT_ENTRY_DIXIACHENG,
		EJZGFT_EXIT_DIXIACHENG,
		EJZGFT_DIXIACHENG_SPECIAL,
		EJZGFT_READ_SHIBEI,
		EJZGFT_AREA_CHUANSONGMEN_ACTIVE,
		EJZGFT_AREA_CHUANSONGMEN_CHUANSONG,
		EJZGFT_JIESUO_MIANJUXIUFUNODE,
		EJZGFT_MAX,
	};

	/// <summary>
	/// Proficiency type
	/// </summary>
	internal enum EProficiency
	{
		FaMu,
		CaiKuang,
		ZhongZhi,
		BuZhuo,
		CaiShou,
		YangZhi,
		TuZai,
		PaoMu,
		QieShi,
		RongLian,
		RouPi,
		FangZhi,
		ZhiTao,
		YanMo,
		QiJu,
		WuQi,
		JiaZhou,
		ZhuBao,
		JianZhu,
		LianJin,
		PengRen,
		Dao,
		ShuangDao,
		Mao,
		Chui,
		QuanTao,
		Gong,
		DaJian,
		PouJie,
		DunPai,
		Max
	}

	internal enum ETanSuoDianType
	{
		ETSD_TYPE_NOT_DEFINE,
		ETSD_TYPE_JINZITA,
		ETSD_TYPE_YIJI,
		ETSD_TYPE_DIXIA_YIJI,
		ETSD_TYPE_YEWAI_YIJI,
		ETSD_TYPE_YEWAI_YIZHI,
		ETSD_TYPE_BULUO_CHENGZHAI_BIG,
		ETSD_TYPE_BULUO_CHENGZHAI_MIDDLE,
		ETSD_TYPE_BULUO_CHENGZHAI_SMALL,
		ETSD_TYPE_CHAOXUE,
		ETSD_TYPE_KUANGCHUANG_BIG,
		ETSD_TYPE_KUANGCHUANG_MIDDLE,
		ETSD_TYPE_DIXIACHENG,
		ETSD_TYPE_CHUANSONGMEN,
		ETSD_TYPE_KUANGCHUANG_SMALL,
		ETSD_TYPE_SHEN_MIAO
	}

	/// <summary>
	/// Weapon type
	/// </summary>
	internal enum EWuQiLeiXing
	{
		WUQI_LEIXING_NONE,
		WUQI_LEIXING_DAO,
		WUQI_LEIXING_MAO,
		WUQI_LEIXING_GONG,
		WUQI_LEIXING_CHUI,
		WUQI_LEIXING_DUN,
		WUQI_LEIXING_QUANTAO,
		WUQI_LEIXING_SHUANGDAO,
		WUQI_LEIXING_JIAN,
		WUQI_LEIXING_TOUZHIWU,
		WUQI_LEIXING_GONGCHENGCHUI,
		WUQI_LEIXING_MAX
	}

	/// <summary>
	/// Sex of character
	/// </summary>
	internal enum EXingBieType
	{
		CHARACTER_XINGBIE_NAN, // Male
		CHARACTER_XINGBIE_NV,  // Female
		CHARACTER_XINGBIE_MAX,
		CHARACTER_XINGBIE_WEIZHI
	}

	/// <summary>
	/// Exntension methods for game enums
	/// </summary>
	internal static class GameEnumExtensions
	{
		public static string ToEn(this EXingBieType value)
		{
			return value switch
			{
				EXingBieType.CHARACTER_XINGBIE_NAN => "Male",
				EXingBieType.CHARACTER_XINGBIE_NV => "Female",
				EXingBieType.CHARACTER_XINGBIE_WEIZHI => "Random", // Technically "Unknown", but means "Random" for spawners
				_ => Default(value)
			};
		}

		/// <summary>
		/// Return an English representation of the value
		/// </summary>
		public static string ToEn(this EClanDiWei value)
		{
			// Values from DT_YiWenText ClanDiWei_#
			return value switch
			{
				EClanDiWei.CLAN_DIWEI_LOW => "Novice",
				EClanDiWei.CLAN_DIWEI_MIDDLE => "Skilled",
				EClanDiWei.CLAN_DIWEI_HIGH => "Master",
				_ => Default(value)
			};
		}

		/// <summary>
		/// Return an English representation of the value
		/// </summary>
		public static string ToEn(this EClanZhiYe value)
		{
			// Values from DT_YiWenText ZhiYe_#
			return value switch
			{
				EClanZhiYe.ZHIYE_TYPE_NONE => "Vagrant",
				EClanZhiYe.ZHIYE_TYPE_WUWEI => "Warrior",
				EClanZhiYe.ZHIYE_TYPE_SHOULIE => "Hunter",
				EClanZhiYe.ZHIYE_TYPE_SHOUHU => "Guard",
				EClanZhiYe.ZHIYE_TYPE_KULI => "Laborer",
				EClanZhiYe.ZHIYE_TYPE_ZAGONG => "Porter",
				EClanZhiYe.ZHIYE_TYPE_ZONGJIANG => "Craftsman",
				_ => Default(value)
			};
		}

		/// <summary>
		/// Return an English representation of the value
		/// </summary>
		public static string ToEn(this EClanType value)
		{
			return value switch
			{
				EClanType.CLAN_TYPE_NONE => "Unaffiliated",
				EClanType.CLAN_TYPE_A => "Claw Tribe",
				EClanType.CLAN_TYPE_B => "Flint Tribe",
				EClanType.CLAN_TYPE_C => "Fang Tribe",
				EClanType.CLAN_TYPE_D => "Plunderer",
				EClanType.CLAN_TYPE_E => "Unknown",
				EClanType.CLAN_TYPE_F => "Unknown",
				EClanType.CLAN_TYPE_INVADER => "Invader",
				_ => Default(value)
			};
		}

		private static string Default(Enum value)
		{
			string valueStr = value.ToString();
			return valueStr.Substring(valueStr.LastIndexOf('_') + 1).ToLowerInvariant();
		}
	}
}
