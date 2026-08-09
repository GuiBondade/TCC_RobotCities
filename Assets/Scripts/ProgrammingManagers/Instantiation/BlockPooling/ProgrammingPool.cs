using UnityEngine;
using UnityEngine.Pool; // Necessário para usar o sistema nativo
using System.Collections.Generic;

public class ProgrammingPool : MonoBehaviour
{
    [System.Serializable]
    public struct BlockNextPoolConfig {
        public BlockUINext prefab;
        public int defaultCapacity;
        public int maxSize;   
    }

    [System.Serializable]
    public struct BlockBodyPoolConfig {
        public BlockUIBody prefab;
        public int defaultCapacity;
        public int maxSize;   
    }

    [System.Serializable]
    public struct CreateVarPoolConfig {
        public CreateVariableUI prefab;
        public int defaultCapacity;
        public int maxSize;   
    }

    [System.Serializable]
    public struct ParamPoolConfig {
        public ReferenceHolder prefab;
        public int defaultCapacity;
        public int maxSize;   
    }

    [System.Serializable]
    public struct SectionPoolConfig {
        public SectionInfo prefab;
        public int defaultCapacity;
        public int maxSize;   
    }

    [System.Serializable]
    public struct ValuePoolConfig {
        public ValueInfo prefab;
        public int defaultCapacity;
        public int maxSize;   
    }

    public BlocksFactory blocksFactory;

    [SerializeField] public BlockNextPoolConfig blockNextConfig;
    [SerializeField] public BlockBodyPoolConfig blockBodyConfig;
    [SerializeField] public BlockNextPoolConfig blockVarConfig;
    [SerializeField] public CreateVarPoolConfig createVarConfig;
    [SerializeField] public ParamPoolConfig parameterConfig;
    [SerializeField] public SectionPoolConfig sectionConfig;
    [SerializeField] public ValuePoolConfig valueConfig;

    private ObjectPool<BlockUINext> blockNextPool;
    private ObjectPool<BlockUIBody> blockBodyPool;
    private ObjectPool<BlockUINext> blockVarPool;
    private ObjectPool<CreateVariableUI> createVarPool;
    private ObjectPool<ReferenceHolder> parameterPool;
    private ObjectPool<SectionInfo> sectionPool;
    private ObjectPool<ValueInfo> valuePool;

    void Awake()
    {
        
        blockNextPool = new ObjectPool<BlockUINext>(
            createFunc: () => Instantiate(blockNextConfig.prefab, this.transform),
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            blockNextConfig.defaultCapacity, 
            blockNextConfig.maxSize
        );

        blockBodyPool = new ObjectPool<BlockUIBody>(
            createFunc: () => Instantiate(blockBodyConfig.prefab, this.transform),
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            blockBodyConfig.defaultCapacity, 
            blockBodyConfig.maxSize
        );

        blockVarPool = new ObjectPool<BlockUINext>(
            createFunc: () => Instantiate(blockVarConfig.prefab, this.transform),
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            blockVarConfig.defaultCapacity, 
            blockVarConfig.maxSize
        );

        createVarPool = new ObjectPool<CreateVariableUI>(
            createFunc: () => Instantiate(createVarConfig.prefab, this.transform),
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            createVarConfig.defaultCapacity, 
            createVarConfig.maxSize
        );

        parameterPool = new ObjectPool<ReferenceHolder>(
            createFunc: () => Instantiate(parameterConfig.prefab, this.transform),
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            parameterConfig.defaultCapacity, 
            parameterConfig.maxSize
        );

        sectionPool = new ObjectPool<SectionInfo>(
            createFunc: () => Instantiate(sectionConfig.prefab, this.transform), 
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            sectionConfig.defaultCapacity, 
            sectionConfig.maxSize
        );

        valuePool = new ObjectPool<ValueInfo>(
            createFunc: () => Instantiate(valueConfig.prefab, this.transform), 
            actionOnGet: go => go.gameObject.SetActive(true), 
            actionOnRelease: go => go.gameObject.SetActive(false), 
            actionOnDestroy: go => Destroy(go), 
            collectionCheck: true,
            valueConfig.defaultCapacity, 
            valueConfig.maxSize
        );
    }

    public BlockUINext GetBlockNext() => blockNextPool.Get();
    public void ReleaseBlockNext(BlockUINext obj) {
        obj.transform.SetParent(this.transform, false);
        blockNextPool.Release(obj);
    }

    public BlockUIBody GetBlockBody() => blockBodyPool.Get();
    public void ReleaseBlockBody(BlockUIBody obj) {
        obj.transform.SetParent(this.transform, false);
        blockBodyPool.Release(obj);
    } 

    public BlockUINext GetBlockVar() => blockVarPool.Get();
    public void ReleaseBlockVar(BlockUINext obj) {
        obj.transform.SetParent(this.transform, false);
        blockVarPool.Release(obj);
    }

    public CreateVariableUI GetCreateVar() => createVarPool.Get();
    public void ReleaseCreateVar(CreateVariableUI obj) {
        obj.transform.SetParent(this.transform, false);
        createVarPool.Release(obj);
    }

    public ReferenceHolder GetParameter() => parameterPool.Get();
    public void ReleaseParameter(ReferenceHolder obj) {
        obj.transform.SetParent(this.transform, false);
        parameterPool.Release(obj);
    } 

    public SectionInfo GetSection() => sectionPool.Get();
    public void ReleaseSection(SectionInfo obj) {
        obj.transform.SetParent(this.transform, false);
        sectionPool.Release(obj);
    } 

    public ValueInfo GetValue() => valuePool.Get();
    public void ReleaseValue(ValueInfo obj) {
        obj.transform.SetParent(this.transform, false);
        valuePool.Release(obj);
    } 
}